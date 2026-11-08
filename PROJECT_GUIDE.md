# School Admission System — Backend Project Guide

Welcome to the School Admission System backend codebase. This project was developed as part of an Assistant Software Engineer recruitment assessment using **C#**, **ASP.NET Core Web API (.NET 10)**, **Entity Framework Core**, **PostgreSQL**, and **xUnit**.

The project follows a clean, beginner-friendly **MVC-style architecture** designed for readability, maintainability, and effortless navigation in Visual Studio.

---

## 1. Project Directory Structure

```text
School Admission System/
│
├── SchoolAdmission.slnx                 # Solution file (Visual Studio 2022+ compatible)
├── .env.example                         # Example environment variables template
├── .env                                 # Local development environment configuration (gitignored)
├── .gitignore                           # Git ignore rules for .NET and sensitive files
├── PROJECT_GUIDE.md                     # Architecture, directory map, and running instructions
├── ASSUMPTIONS.md                       # Architectural and business assumptions documentation
│
└── backend/
    │
    ├── SchoolAdmission.Api/             # Main ASP.NET Core Web API project
    │   ├── Controllers/                 # API controllers handling HTTP requests and routing
    │   │   ├── ClassesController.cs     # Endpoints 1 to 4: /classes, /classes/{classId}, /classes/{classId}/holds
    │   │   ├── HoldsController.cs       # Endpoints 5 to 7: /holds/{holdId}, /holds/{holdId}/confirm, DELETE /holds/{holdId}
    │   │   └── MeController.cs          # Endpoint 8: GET /me/holds (current student's applications)
    │   │
    │   ├── Models/                      # Core domain entities and constants
    │   │   ├── ClassEntity.cs           # Classes table entity (Id, Name, TotalSeats)
    │   │   ├── AdmissionHold.cs         # AdmissionHolds table entity (Id, ClassId, StudentId, Status, CreatedAt, ExpiresAt)
    │   │   └── HoldStatus.cs            # Constants for statuses: HELD, CONFIRMED, EXPIRED, CANCELLED
    │   │
    │   ├── Data/                        # EF Core database context and table configurations
    │   │   └── ApplicationDbContext.cs  # DbContext with check constraints, foreign keys, and indexes
    │   │
    │   ├── Services/                    # Business logic and background services
    │   │   ├── IAdmissionService.cs     # Service contract for admission operations
    │   │   ├── AdmissionService.cs      # Core business logic: seat calculation, transactions, row locking
    │   │   └── HoldCleanupBackgroundService.cs # Periodic background worker for automatic hold expiration
    │   │
    │   ├── DTOs/                        # Data Transfer Objects for API request and response contracts
    │   │   ├── CreateClassRequest.cs    # Request body for POST /classes
    │   │   ├── ClassResponse.cs         # Response for GET /classes and POST /classes
    │   │   ├── CreateHoldRequest.cs     # Optional payload for hold creation
    │   │   ├── HoldResponse.cs          # Response contract for reservation endpoints
    │   │   └── ErrorResponse.cs         # Standardized error response contract
    │   │
    │   ├── Migrations/                  # EF Core database migrations
    │   │   └── 20261108165705_InitialCreate.cs
    │   │
    │   ├── Properties/
    │   │   └── launchSettings.json      # Visual Studio launch profiles and URLs
    │   │
    │   ├── Program.cs                   # Application entry point, DI setup, middleware, .env loading
    │   └── appsettings.json             # Configuration settings template
    │
    └── SchoolAdmission.Tests/           # Automated test suite using xUnit
        ├── TestWebApplicationFactory.cs # In-memory test server setup configured with PostgreSQL
        ├── ClassTests.cs                # Tests for class creation, listing, and validation
        ├── HoldReservationTests.cs      # Tests for hold reservation, duplicate applications, full class
        ├── HoldConfirmationTests.cs     # Mandatory Test 2 (duplicate confirmation idempotency) and ownership
        ├── HoldCancellationTests.cs     # Tests for hold cancellation, releasing seats, and ownership
        ├── HoldExpirationTests.cs       # Mandatory Test 1 (automatic seat release on expiration)
        ├── ConcurrencyTests.cs          # Mandatory Test 3 (high-concurrency race condition testing)
        ├── StudentHoldsTests.cs         # Tests for GET /me/holds endpoint
        └── xunit.runner.json            # Test runner configuration (isolated execution)
```

---

## 2. Key Component Locations

| Component | Path | Description |
|---|---|---|
| **Controllers** | `backend/SchoolAdmission.Api/Controllers/` | Handles routing, input validation, HTTP headers (`X-Student-Id`), and maps status codes (200, 201, 204, 400, 403, 404, 409). |
| **Models** | `backend/SchoolAdmission.Api/Models/` | Domain entities mapped to PostgreSQL tables (`Classes`, `AdmissionHolds`). |
| **Data (DbContext)** | `backend/SchoolAdmission.Api/Data/` | `ApplicationDbContext` configuring relationships, indexes, constraints, and cascading rules. |
| **Business Logic** | `backend/SchoolAdmission.Api/Services/` | Implements seat availability formulas, PostgreSQL transactions, row-level pessimistic locking (`FOR UPDATE`), and hold lifecycle. |
| **DTOs** | `backend/SchoolAdmission.Api/DTOs/` | Strongly typed models for request bodies and response formatting. |
| **Migrations** | `backend/SchoolAdmission.Api/Migrations/` | EF Core code-first migration files for creating and versioning PostgreSQL tables. |
| **Tests** | `backend/SchoolAdmission.Tests/` | 20 automated tests covering all mandatory rules, expiration, duplicate confirmation, and concurrency. |
| **Database Connection** | `.env` / Environment Variables | Kept strictly outside source code in `.env` / environment variable `DB_CONNECTION_STRING`. |

---

## 3. Database Design & Concurrency Control

### Tables

1. **Classes**
   - `Id` (string, PK, e.g., `c_70c72`)
   - `Name` (string, required, e.g., "Class 6 - Morning Shift")
   - `TotalSeats` (int, check constraint `TotalSeats > 0`)

2. **AdmissionHolds**
   - `Id` (string, PK, e.g., `h_257d3`)
   - `ClassId` (string, FK -> `Classes.Id`)
   - `StudentId` (string, required)
   - `Status` (string, required: `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`)
   - `CreatedAt` (DateTime UTC)
   - `ExpiresAt` (DateTime UTC)

### Concurrency Protection & Row-Level Locking

To guarantee that simultaneous reservation requests never exceed class capacity (`heldSeats + confirmedSeats <= totalSeats`), `AdmissionService.CreateHoldAsync`:

1. Begins a database transaction with `ReadCommitted` isolation.
2. Executes a row-level lock on the target class row:
   ```sql
   SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE
   ```
3. PostgreSQL blocks any concurrent reservation transaction targeting the same class until the active transaction commits.
4. Active holds (`Status == 'HELD' AND ExpiresAt > now`) and confirmed admissions (`Status == 'CONFIRMED'`) are calculated.
5. If seats remain, the new hold is inserted and committed.
6. The next waiting transaction then acquires the lock and sees the newly committed hold, preventing any overbooking or race condition.

---

## 4. How to Open and Run in Visual Studio

1. Open **Visual Studio 2022** (v17.10 or newer) or latest Visual Studio.
2. Click **File -> Open -> Project/Solution**.
3. Select `SchoolAdmission.slnx` (or `SchoolAdmission.sln`) from the repository root:
   `D:\Job\School Admission System\SchoolAdmission.slnx`
4. In **Solution Explorer**, right-click `SchoolAdmission.Api` and select **Set as Startup Project**.
5. Ensure your `.env` file exists at the solution root or `backend/SchoolAdmission.Api/` with your PostgreSQL credentials:
   ```env
   DB_CONNECTION_STRING=Host=localhost;Port=5433;Database=school_admission;Username=postgres;Password=postgres
   HOLD_DURATION_SECONDS=600
   HOLD_CLEANUP_INTERVAL_SECONDS=10
   ```
6. Press **F5** (or **Ctrl+F5**) to run.
7. The application will start and listen on `http://localhost:5119`. You can view and interact with endpoints via the OpenAPI specification.

---

## 5. How to Run Migrations

To apply EF Core migrations using the .NET CLI:

1. Open PowerShell or Terminal in the repository root:
   ```powershell
   # Apply migrations to the primary database
   dotnet-ef database update --project backend/SchoolAdmission.Api/SchoolAdmission.Api.csproj
   ```

2. To generate a new migration after modifying models:
   ```powershell
   dotnet-ef migrations add <MigrationName> --project backend/SchoolAdmission.Api/SchoolAdmission.Api.csproj
   ```

---

## 6. How to Run Automated Tests

The automated test suite runs against the PostgreSQL test instance `school_admission_test`:

1. Open PowerShell in the repository root.
2. Run all tests:
   ```powershell
   dotnet test SchoolAdmission.slnx
   ```
3. To run tests with detailed output:
   ```powershell
   dotnet test SchoolAdmission.slnx -l "console;verbosity=detailed"
   ```

---

## 7. Required API Endpoints Summary

| Method | Endpoint | Description | Expected Status Codes |
|---|---|---|---|
| `POST` | `/classes` | Create a new class with seat capacity | `201 Created`, `400 Bad Request` |
| `GET` | `/classes` | List all classes with seat counts | `200 OK` |
| `GET` | `/classes/{classId}` | Get single class with seat counts | `200 OK`, `404 Not Found` |
| `POST` | `/classes/{classId}/holds` | Create temporary seat reservation (`X-Student-Id` header) | `201 Created`, `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `GET` | `/holds/{holdId}` | Get reservation status and expiration | `200 OK`, `404 Not Found` |
| `POST` | `/holds/{holdId}/confirm` | Confirm admission (`X-Student-Id` header) | `200 OK`, `400 Bad Request`, `403 Forbidden`, `404 Not Found` |
| `DELETE` | `/holds/{holdId}` | Cancel reservation (`X-Student-Id` header) | `204 NoContent`, `400 Bad Request`, `403 Forbidden`, `404 Not Found` |
| `GET` | `/me/holds` | List student's applications (`X-Student-Id` header) | `200 OK`, `400 Bad Request` |
