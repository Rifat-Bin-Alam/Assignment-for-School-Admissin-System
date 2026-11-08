# Architectural & Business Assumptions

This document outlines the architectural decisions, trade-offs, and design assumptions made during the backend development of the School Admission System.

---

## 1. Identifier Generation Strategy

* **Context:** The prompt examples showed `"classId": "c_123"` and `"holdId": "h_789"`.
* **Assumption / Decision:**
  - Class IDs are formatted as `c_` + short unique hex string (e.g., `c_70c72`).
  - Hold IDs are formatted as `h_` + short unique hex string (e.g., `h_257d3`).
* **Rationale:**
  - String identifiers with prefixes match the exact JSON examples in the specification.
  - Generating unique strings prevents key collision issues across concurrent test suites and multi-threaded requests while remaining concise and human-readable.

---

## 2. Student Identity & `X-Student-Id` Header

* **Context:** The specification states: *"Use the X-Student-Id header where specified in the assignment."*
* **Assumption / Decision:**
  - `POST /classes/{classId}/holds` reads `X-Student-Id` header (with optional fallback to request body `studentId`).
  - `POST /holds/{holdId}/confirm` requires `X-Student-Id` header.
  - `DELETE /holds/{holdId}` requires `X-Student-Id` header.
  - `GET /me/holds` requires `X-Student-Id` header.
  - If the header is missing, empty, or whitespace, the API immediately returns `400 Bad Request` with `{"message": "X-Student-Id header is required."}`.
* **Rationale:**
  - Eliminates ambiguity and ensures invalid requests fail with descriptive 4xx responses rather than 500 server errors.

---

## 3. Application Ownership Enforcement (Rule 8)

* **Context:** Rule 8 states: *"A student can only confirm their own reservation."*
* **Assumption / Decision:**
  - If Student B attempts to confirm or cancel Student A's reservation, the API returns `403 Forbidden` (`{"message": "You can only confirm your own reservation."}`).
* **Rationale:**
  - In standard HTTP/REST design, `403 Forbidden` specifically indicates that the client is recognized/identified (via header) but lacks authorization for the requested resource.

---

## 4. Idempotent Confirmation (Rule 6)

* **Context:** Rule 6 states: *"Confirming the same application multiple times must not increase confirmedSeats more than once."*
* **Assumption / Decision:**
  - If a reservation is already in `CONFIRMED` status and `POST /holds/{holdId}/confirm` is called again by the same student, the API returns `200 OK` with the existing confirmed record.
  - It does not increase confirmed seats or produce an error.
* **Rationale:**
  - This provides idempotent behavior for network retries and duplicate user clicks in frontend interfaces.

---

## 5. Dual-Layer Hold Expiration (Rule 4)

* **Context:** Rule 4 states: *"When a hold expires, its seat must become available automatically. No manual API request should be necessary to release an expired seat."*
* **Assumption / Decision:**
  - We implemented a dual-layer expiration strategy:
    1. **Real-time Query Filtering:** `GET /classes` and `GET /classes/{classId}` strictly count `heldSeats` as `Status == 'HELD' AND ExpiresAt > now`. Any expired hold instantly releases its seat mathematically, with zero lag and no dependency on background worker timing.
    2. **Background Cleanup Worker (`HoldCleanupBackgroundService`):** A hosted background service runs every 10 seconds (configurable via `HOLD_CLEANUP_INTERVAL_SECONDS`) to update database rows from `HELD` to `EXPIRED`.
    3. **On-Demand Expiration:** During `POST /classes/{classId}/holds` and `POST /holds/{holdId}/confirm`, any expired holds are updated to `EXPIRED` atomically.
* **Rationale:**
  - Prevents race conditions and guarantees that seats are released the exact millisecond `ExpiresAt` is reached, even if the background timer hasn't fired yet.

---

## 6. Re-Applying After Expiration or Cancellation (Rule 7)

* **Context:** Rule 7 states: *"A student cannot have multiple active applications or confirmed admissions in the same class."*
* **Assumption / Decision:**
  - If a student's prior hold expired or was cancelled, they are permitted to submit a new application for the same class if seats are available.
  - A PostgreSQL partial unique index (`"ClassId", "StudentId"` WHERE `"Status" IN ('HELD', 'CONFIRMED')`) is enforced at the database level.
* **Rationale:**
  - A student should not be permanently locked out of applying if their previous hold naturally expired or was cancelled by them. Only active (`HELD` and unexpired) or `CONFIRMED` statuses prevent re-application.

---

## 7. PostgreSQL Row-Level Concurrency Control (Rule 12)

* **Context:** Rule 12 states: *"Handle simultaneous requests correctly. Multiple students must not reserve more seats than the class capacity. Use proper PostgreSQL transactions and concurrency control. Do not depend only on checking available seats in C# before saving."*
* **Assumption / Decision:**
  - We use explicit database transactions with PostgreSQL's `SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE` row locking.
* **Rationale:**
  - Optimistic concurrency (e.g. RowVersion) fails under high contention because all losing transactions throw exceptions and must be retried.
  - Pessimistic row-level locking (`FOR UPDATE`) cleanly serializes seat checks per individual class row, allowing winning transactions to commit in order and rejecting surplus requests immediately with HTTP 409 Conflict.
  - Row-level locks on Class A do not block reservations for Class B, maintaining high throughput across different classes.

---

## 8. Cancellation of Confirmed Admissions

* **Context:** The prompt specifies `DELETE /holds/{holdId}` as *"Cancel a reservation"*.
* **Assumption / Decision:**
  - Calling `DELETE /holds/{holdId}` on a `HELD` hold sets its status to `CANCELLED` and returns `204 NoContent`, releasing the seat.
  - Calling `DELETE /holds/{holdId}` on an already cancelled hold is idempotent and returns `204 NoContent`.
  - Non-existent hold IDs return `404 NotFound`.
* **Rationale:**
  - Consistent RESTful semantics and idempotent operations for safe client retries.
