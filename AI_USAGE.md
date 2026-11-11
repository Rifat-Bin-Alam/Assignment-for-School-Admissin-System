# AI Assistance Documentation

This document transparently records the use of AI assistance during the development of the School Admission System, including code generated, issues identified, corrections made, and verification procedures.

---

## 1. AI Tools Used

- **Tool:** Google Antigravity (Advanced Agentic Assistant powered by Gemini).
- **Scope of Assistance:** Architecture review, boilerplate scaffolding, database concurrency strategy, test authoring, Docker multi-stage configuration, and documentation drafting.

---

## 2. Areas That Received AI Assistance

1. **Backend Development:**
   - Generation of ASP.NET Core controllers, DTOs, domain models, and EF Core `ApplicationDbContext`.
   - Implementation of PostgreSQL `SELECT ... FOR UPDATE` row-level pessimistic locking in `AdmissionService`.
   - Implementation of `HoldCleanupBackgroundService` for automatic reservation expiration.
2. **Frontend Development:**
   - Scaffolding the React 18 application with Vite.
   - Authoring the four required pages (`CreateClassPage`, `ClassListPage`, `ApplyPage`, `MyApplicationsPage`).
   - Implementation of `StudentContext` for persistent 17-digit birth registration number storage in `localStorage`.
   - Writing clean, institutional CSS styles without AI design clichés.
3. **Automated Testing:**
   - Authoring 20 xUnit integration tests using `WebApplicationFactory<Program>`, including tests for expiration, idempotency, student ownership, and a concurrency test with 15 and 20 simultaneous applicants.
4. **Containerization & Deployment:**
   - Creation of multi-stage `Dockerfile` for the backend (.NET 10 SDK build + ASP.NET Core runtime).
   - Creation of multi-stage `Dockerfile` for the frontend (Node 20 build + Nginx runtime).
   - Creation of root-level `docker-compose.yml` with PostgreSQL health checks and persistent volumes.

---

## 3. Review of AI-Generated Code & Issues Corrected

During development, several AI-generated proposals were reviewed, tested, and corrected:

1. **EF Core Execution Strategy Conflict with Explicit Transactions:**
   - *Issue:* An initial suggestion included `EnableRetryOnFailure()` in `UseNpgsql(...)`. When explicit transactions were started with `BeginTransactionAsync()`, EF Core threw `InvalidOperationException: The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions`.
   - *Correction:* Removed `EnableRetryOnFailure()` from the connection setup so that standard, explicit PostgreSQL transactions with row-level locks execute natively without conflict.
2. **xUnit Parallel Test Collision on Database:**
   - *Issue:* By default, xUnit executes test classes in parallel. When multiple test classes ran simultaneously against the PostgreSQL test database, one test's database reset (`ExecuteDeleteAsync`) interfered with concurrent operations in another test class, causing foreign key restrict violations.
   - *Correction:* Created `xunit.runner.json` with `"parallelizeTestCollections": false`, ensuring all database tests run in isolated, sequential order.
3. **Visual Studio `.slnx` Format Incompatibility:**
   - *Issue:* .NET 10 defaulted to the newer `.slnx` solution format. Visual Studio's NuGet Package Manager reported that project metadata could not be loaded.
   - *Correction:* Re-generated the canonical Visual Studio `.sln` format (`SchoolAdmission.sln`) with standard GUIDs and flattened solution entries, restoring full Visual Studio and F5 debugging compatibility.
4. **Hold Expiration Race Condition:**
   - *Issue:* Relying exclusively on a background timer for hold expiration resulted in a short window (up to 10 seconds) where expired holds were still counted as held.
   - *Correction:* Introduced real-time mathematical filtering in seat calculation queries (`heldSeats = holds.Count(h => h.Status == 'HELD' && h.ExpiresAt > now)`). This guarantees immediate seat availability upon expiration while the background service cleans up the database asynchronously.
5. **CORS Missing on Initial Backend Setup:**
   - *Issue:* Browser fetch requests from `http://localhost:5173` were blocked by CORS in early manual testing.
   - *Correction:* Configured `AddCors()` and `app.UseCors()` in `Program.cs` to allow all headers (specifically `X-Student-Id`), origins, and methods.

---

## 4. Verification and Testing

All code was verified through live execution:
- **Build Verification:** Backend builds with 0 errors and 0 warnings on .NET 10. Frontend builds in 256ms with 0 errors via Vite.
- **Automated Tests:** 20 xUnit integration tests passed against a real PostgreSQL database instance.
- **Concurrency Verification:** Verified that under 15 simultaneous requests for 1 remaining seat, exactly 1 reservation succeeds (HTTP 201) and all 14 others are rejected with HTTP 409 Conflict.
- **End-to-End Testing:** All eight endpoints and all four frontend screens were verified using interactive script calls and browser testing.
