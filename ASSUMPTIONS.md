# Assumptions & Technical Decisions

This document outlines key technical decisions and assumptions made where the assignment specification did not provide explicit details.

---

## 1. Concurrency Control Mechanism
- **Requirement:** Multiple students must not reserve more seats than class capacity under concurrent requests. Concurrency safety must not rely solely on in-memory C# checks.
- **Decision:** Implemented PostgreSQL pessimistic row-level locking (`SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE`) inside an explicit database transaction with `ReadCommitted` isolation.
- **Reason:** In high-concurrency scenarios, optimistic concurrency checks cause failed commits and unhandled concurrency exceptions that require complex client-side retry logic. A database-level row lock on the class record guarantees serialized seat evaluation and reservation in PostgreSQL, completely eliminating overbooking race conditions while allowing concurrent reservations for different classes to execute in parallel.

---

## 2. Immediate Mathematical Expiration vs Background Polling
- **Requirement:** When a hold expires, its seat must become available automatically without requiring a manual release API call.
- **Decision:** Designed a dual-layer expiration model:
  1. *Real-time evaluation:* The seat calculation formula (`availableSeats = totalSeats - (heldSeats + confirmedSeats)`) counts held seats strictly where `Status == 'HELD' AND ExpiresAt > now`. If a hold expires, its seat is immediately treated as available on all GET endpoints without waiting for a database update.
  2. *Background cleanup:* A hosted `BackgroundService` runs periodically (every 10 seconds by default) to transition expired `HELD` rows to `EXPIRED` in the database. Relevant endpoints also check and update expired holds on-demand.
- **Reason:** Background timers introduce polling latency (e.g., 5–10 seconds). Calculating active seats dynamically ensures zero-lag availability while the background worker maintains database hygiene.

---

## 3. Student Identification and Authentication
- **Requirement:** Use the student's 17-digit Birth Registration Number as an identifier without creating login or password mechanisms.
- **Decision:** Used the `X-Student-Id` HTTP header on all student-specific requests (`POST /classes/{id}/holds`, `POST /holds/{id}/confirm`, `DELETE /holds/{id}`, `GET /me/holds`). The frontend stores this ID in `localStorage` and exposes a top navigation input so it can be updated or reused across pages.
- **Reason:** Eliminates the overhead of user registration, sessions, and JWT tokens while satisfying the assignment requirement for student identity persistence.

---

## 4. Duplicate Confirmation (Idempotency)
- **Requirement:** Confirming the same application multiple times must not increase confirmed seats more than once.
- **Decision:** If a hold is already in `CONFIRMED` status, subsequent confirmation requests by the same student return HTTP `200 OK` with the existing confirmed record instead of returning an error.
- **Reason:** Follows RESTful idempotency best practices. Prevents double-booking from network retries or accidental double-clicks in the browser.

---

## 5. Reverse Proxy with Nginx in Docker
- **Requirement:** Production-ready frontend container communicating with the backend.
- **Decision:** Configured Nginx inside the frontend container to serve the static Vite build, handle React Router routes (`try_files $uri /index.html`), and reverse proxy API routes (`/classes`, `/holds`, `/me`) to `http://backend:8080`.
- **Reason:** Avoids cross-origin issues inside container networks and ensures production web server performance.

---

## 6. Automatic EF Core Database Migration at Startup
- **Requirement:** The entire application must start with `docker compose up --build` without requiring manual database creation or migration commands.
- **Decision:** Added `db.Database.Migrate()` inside `Program.cs` before accepting HTTP requests, combined with Docker Compose's `depends_on: database: condition: service_healthy`.
- **Reason:** Ensures that whenever the backend container starts or connects to a fresh PostgreSQL database, the schema and tables are automatically created and updated without manual CLI steps.
