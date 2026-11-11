# Project Guide — Developer Reference

This guide provides a comprehensive file-by-file map of the School Admission System codebase to assist with code navigation, architecture review, and development.

---

## 1. File Location Map

### Root Orchestration & Configuration
- **`docker-compose.yml`**: Defines the three-tier architecture (`database`, `backend`, `frontend`), service dependencies, health checks, environment variables, network, and persistent storage volume (`school_admission_postgres_data`).
- **`SchoolAdmission.sln`**: Visual Studio Solution file linking the API and test projects.
- **`.env.example`**: Example template for environment variables used by Docker and local setups.
- **`.gitignore`**: Ignores compiled binaries, build artifacts, node modules, temporary databases, and environment secrets.

---

## 2. Backend Architecture (`backend/`)

- **`backend/Dockerfile`**: Multi-stage build for ASP.NET Core:
  - Stage 1 (`build`): Restores NuGet packages and compiles `SchoolAdmission.Api` using the .NET 10 SDK.
  - Stage 2 (`runtime`): Copies the compiled binary into the lightweight ASP.NET Core 10 runtime image, exposes port `8080`, and sets the entry point.

### `backend/SchoolAdmission.Api/`
- **`Program.cs`**:
  - Configures services: EF Core with PostgreSQL, scoped `AdmissionService`, hosted background service `HoldCleanupBackgroundService`, CORS policies, and OpenAPI.
  - Contains automatic EF Core migration startup logic: `db.Database.Migrate()`.
  - Global error handling middleware ensuring 4xx/500 JSON formatted responses.
- **`appsettings.json`**: Base configuration settings and fallback connection strings.
- **`Controllers/`**:
  - **`ClassesController.cs`**: Handles `POST /classes`, `GET /classes`, `GET /classes/{classId}`, and `POST /classes/{classId}/holds`. Extracts the `X-Student-Id` header.
  - **`HoldsController.cs`**: Handles `GET /holds/{holdId}`, `POST /holds/{holdId}/confirm`, and `DELETE /holds/{holdId}`. Enforces student ownership.
  - **`MeController.cs`**: Handles `GET /me/holds`, returning all applications for the student specified in `X-Student-Id`.
- **`Models/`**:
  - **`ClassEntity.cs`**: Entity representing a specific class and shift combination (`Id`, `Name`, `TotalSeats`).
  - **`AdmissionHold.cs`**: Entity representing a seat hold reservation or confirmed admission (`Id`, `ClassId`, `StudentId`, `Status`, `CreatedAt`, `ExpiresAt`).
  - **`HoldStatus.cs`**: Constants defining the four allowed status values: `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`.
- **`Data/ApplicationDbContext.cs`**:
  - Configures table names (`Classes`, `AdmissionHolds`), check constraint `TotalSeats > 0`, foreign key relationships, indexes, and a partial unique index on `(ClassId, StudentId)` for active holds.
- **`Migrations/`**:
  - Code-first EF Core database migration files for PostgreSQL schema versioning.
- **`Services/`**:
  - **`IAdmissionService.cs`**: Interface declaring core admission and reservation business operations.
  - **`AdmissionService.cs`**: Implements seat availability checking, PostgreSQL pessimistic row-level locking (`SELECT ... FOR UPDATE`), transaction management, and hold lifecycle transitions.
  - **`HoldCleanupBackgroundService.cs`**: Periodic background task scanning the database to transition expired `HELD` rows to `EXPIRED`.
- **`DTOs/`**:
  - **`CreateClassRequest.cs`**: Request body validation for class creation.
  - **`ClassResponse.cs`**: Data transfer object for classes with dynamic seat counts.
  - **`CreateHoldRequest.cs`**: Optional body payload for hold creation.
  - **`HoldResponse.cs`**: Response format for hold reservations and status checks.
  - **`ErrorResponse.cs`**: Standardized JSON error response format.

### `backend/SchoolAdmission.Tests/`
- **`TestWebApplicationFactory.cs`**: Custom test server factory configured to connect to an isolated PostgreSQL test database (`school_admission_test`).
- **`ClassTests.cs`**: Tests class creation, validation, and retrieval.
- **`HoldReservationTests.cs`**: Tests reservation creation, missing student ID, duplicate student holds, and full capacity errors.
- **`HoldConfirmationTests.cs`**: Tests successful confirmation, student ownership checks, expired hold rejection, and duplicate confirmation idempotency (Mandatory Test 2).
- **`HoldCancellationTests.cs`**: Tests hold cancellation, seat release, and student ownership checks.
- **`HoldExpirationTests.cs`**: Tests automatic seat availability upon hold expiration without manual release calls (Mandatory Test 1).
- **`ConcurrencyTests.cs`**: Dispatches simultaneous reservation requests to a class with 1 available seat to verify that exactly 1 succeeds and capacity is not exceeded (Mandatory Test 3).
- **`StudentHoldsTests.cs`**: Tests the `/me/holds` endpoint with valid and missing headers.

---

## 3. Frontend Architecture (`frontend/`)

- **`frontend/Dockerfile`**: Multi-stage build:
  - Stage 1 (`build`): Installs dependencies, sets `VITE_API_BASE_URL`, and builds the React application with Vite.
  - Stage 2 (`runtime`): Copies production build artifacts into `nginx:alpine` and starts Nginx.
- **`nginx.conf`**: Configures Nginx to serve `index.html` for client-side React Router navigation, and proxies API calls to `http://backend:8080`.
- **`src/services/api.js`**: Universal client using the native Fetch API to communicate with all 8 backend endpoints. Includes error extraction and status code handling.
- **`src/components/`**:
  - **`Navbar.jsx`**: Main navigation header containing links and the persistent 17-digit student birth registration number input.
  - **`StudentContext.jsx`**: React Context providing `studentId`, validation state, and automatic `localStorage` synchronization.
  - **`StatusBadge.jsx`**: Visual badge component displaying color-coded statuses (`HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`).
- **`src/pages/`**:
  - **`CreateClassPage.jsx`** (`/admin/classes/new`): Administrator screen to create new class/shift records.
  - **`ClassListPage.jsx`** (`/classes`): Class listing screen with live seat availability polling every 5 seconds.
  - **`ApplyPage.jsx`** (`/classes/:classId/apply`): Seat reservation screen with countdown timer, simulated payment button, and cancellation button.
  - **`MyApplicationsPage.jsx`** (`/my-applications`): Student application tracker based on their 17-digit birth registration number.
- **`src/styles/main.css`**: Clean, accessible CSS following institutional design requirements.
- **`src/App.jsx`**: Root application router and layout shell.
- **`src/main.jsx`**: Entry point wrapping the app with `BrowserRouter`.
