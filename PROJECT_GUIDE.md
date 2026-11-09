# School Admission System — Full Stack Project Guide

Welcome to the School Admission System codebase. This project was developed as an Assistant Software Engineer recruitment assessment consisting of:
1. **Backend**: ASP.NET Core Web API (.NET 10), Entity Framework Core, PostgreSQL, and xUnit.
2. **Frontend**: React 18, Vite, JavaScript, CSS, and React Router.

The project strictly adheres to an intuitive, beginner-friendly architecture without unnecessary design patterns or enterprise bloat.

---

## 1. Project Directory Structure

```text
School Admission System/
│
├── SchoolAdmission.sln                  # Standard Visual Studio solution file
├── .env.example                         # Environment variables template for backend
├── .env                                 # Local backend secrets & DB settings (gitignored)
├── .gitignore                           # Git ignore rules for .NET, Node, and secrets
├── PROJECT_GUIDE.md                     # Full-stack developer guide and running instructions
├── ASSUMPTIONS.md                       # Architectural decisions and business rules documentation
│
├── backend/
│   ├── SchoolAdmission.Api/             # ASP.NET Core Web API project
│   │   ├── Controllers/                 # REST controllers
│   │   │   ├── ClassesController.cs     # Endpoints 1 to 4: /classes, /classes/{classId}, /classes/{classId}/holds
│   │   │   ├── HoldsController.cs       # Endpoints 5 to 7: /holds/{holdId}, /holds/{holdId}/confirm, DELETE /holds/{holdId}
│   │   │   └── MeController.cs          # Endpoint 8: GET /me/holds
│   │   ├── Models/                      # Core domain entities
│   │   │   ├── ClassEntity.cs           # "Classes" table
│   │   │   ├── AdmissionHold.cs         # "AdmissionHolds" table
│   │   │   └── HoldStatus.cs            # Constants: HELD, CONFIRMED, EXPIRED, CANCELLED
│   │   ├── Data/
│   │   │   └── ApplicationDbContext.cs  # EF Core DbContext with constraints & indexes
│   │   ├── Services/
│   │   │   ├── IAdmissionService.cs     # Admission service contract
│   │   │   ├── AdmissionService.cs      # Core business logic, transactions, row locking
│   │   │   └── HoldCleanupBackgroundService.cs # Periodic background expiration worker
│   │   ├── DTOs/                        # Request & response contracts
│   │   │   ├── CreateClassRequest.cs
│   │   │   ├── ClassResponse.cs
│   │   │   ├── CreateHoldRequest.cs
│   │   │   ├── HoldResponse.cs
│   │   │   └── ErrorResponse.cs
│   │   ├── Migrations/                  # EF Core database migrations
│   │   ├── Properties/
│   │   │   └── launchSettings.json      # Launch profiles (listening on http://localhost:5119)
│   │   ├── Program.cs                   # Entry point, DI, CORS, middleware, .env loader
│   │   └── appsettings.json             # Configuration settings template
│   │
│   └── SchoolAdmission.Tests/           # Automated test suite (20 xUnit tests)
│       ├── TestWebApplicationFactory.cs # TestServer setup against real PostgreSQL instance
│       ├── ClassTests.cs                # Class creation and listing tests
│       ├── HoldReservationTests.cs      # Seat reservation tests
│       ├── HoldConfirmationTests.cs     # Duplicate confirmation idempotency & ownership tests
│       ├── HoldCancellationTests.cs     # Seat release and cancellation tests
│       ├── HoldExpirationTests.cs       # Automatic expiration seat release (Mandatory Test 1)
│       ├── ConcurrencyTests.cs          # 15 & 20 simultaneous applicants (Mandatory Test 3)
│       ├── StudentHoldsTests.cs         # Student holds lookup tests
│       └── xunit.runner.json            # Non-parallelized test collection runner configuration
│
└── frontend/                            # React 18 single-page application
    ├── public/                          # Static assets and favicon
    ├── src/
    │   ├── components/                  # Reusable UI components & context
    │   │   ├── Navbar.jsx               # Navigation bar with 17-digit student ID input
    │   │   ├── StudentContext.jsx       # Student ID state & localStorage persistence
    │   │   └── StatusBadge.jsx          # Semantic status badge (HELD, CONFIRMED, etc.)
    │   ├── pages/                       # Four required UI screens
    │   │   ├── CreateClassPage.jsx      # Page 1: /admin/classes/new
    │   │   ├── ClassListPage.jsx        # Page 2: /classes (live seat updates every 5s)
    │   │   ├── ApplyPage.jsx            # Page 3: /classes/:classId/apply (reserve & payment)
    │   │   └── MyApplicationsPage.jsx   # Page 4: /my-applications (student applications)
    │   ├── services/
    │   │   └── api.js                   # Universal Fetch API service with backend URL
    │   ├── styles/
    │   │   └── main.css                 # Clean, institutional stylesheet (no excessive CSS)
    │   ├── App.jsx                      # App router and layout shell
    │   └── main.jsx                     # Vite entry point with BrowserRouter
    ├── .env.example                     # Example frontend environment variables
    ├── .env                             # Frontend local configuration (VITE_API_BASE_URL)
    ├── index.html                       # HTML document template
    ├── package.json                     # Frontend scripts and dependencies
    └── vite.config.js                   # Vite configuration
```

---

## 2. Component & File Location Map

### Backend

| Component | Path | Description |
|---|---|---|
| **Controllers** | `backend/SchoolAdmission.Api/Controllers/` | HTTP routing, status codes, input validation. |
| **Models** | `backend/SchoolAdmission.Api/Models/` | Domain entities mapped to PostgreSQL (`Classes`, `AdmissionHolds`). |
| **Database Context** | `backend/SchoolAdmission.Api/Data/ApplicationDbContext.cs` | Check constraints, foreign keys, and indexes. |
| **Business Logic** | `backend/SchoolAdmission.Api/Services/` | Implements seat formulas, row-level locking (`SELECT ... FOR UPDATE`), and hold lifecycle. |
| **Tests** | `backend/SchoolAdmission.Tests/` | 20 xUnit tests verifying business rules, concurrency, and expiration. |

### Frontend

| Component | Path | Description |
|---|---|---|
| **Pages** | `frontend/src/pages/` | Four main screens (`CreateClassPage`, `ClassListPage`, `ApplyPage`, `MyApplicationsPage`). |
| **Components** | `frontend/src/components/` | Reusable components (`Navbar`, `StatusBadge`, `StudentContext`). |
| **API Service** | `frontend/src/services/api.js` | Universal Fetch API client communicating with backend endpoints. |
| **Styles (CSS)** | `frontend/src/styles/main.css` | Simple, readable institutional CSS without artificial AI decorations. |
| **Backend URL** | `frontend/.env` | Configured via `VITE_API_BASE_URL=http://localhost:5119`. |

---

## 3. UI Screens & Browser Page Titles

| Screen | URL | Browser Title | Heading | Description |
|---|---|---|---|---|
| **Page 1: Create Class** | `/admin/classes/new` | `Create Class \| School Admission` | `Create Class` | Admin form to create class/shift with seat capacity. |
| **Page 2: Class List** | `/classes` | `Classes \| School Admission` | `Available Classes` | Live table of classes; auto-refreshes seat counts every 5 seconds. |
| **Page 3: Application** | `/classes/:classId/apply` | `Apply for Admission \| School Admission` | `Apply for Admission` | Holds a seat, live countdown timer, simulated payment button, cancellation button. |
| **Page 4: My Applications**| `/my-applications` | `My Applications \| School Admission` | `My Applications` | Lookup applications by 17-digit student ID; confirm or cancel reservations. |

---

## 4. Student Identification Strategy

- **No login or password requirement**: The system identifies students by their **17-digit Birth Registration Number**.
- **Top Navigation Bar Input**: A clearly visible student ID input is located on the top right of the application header.
- **Persistence**: Preserved in `localStorage` (`school_admission_student_id`) across page refreshes and browser restarts.
- **Validation**: Strict validation ensuring exactly 17 numeric digits (`/^\d{17}$/`).
- **HTTP Header**: Automatically attached as the `X-Student-Id` header on all relevant API requests.

---

## 5. How to Run the Entire Application Locally

### Step 1: Start PostgreSQL

Ensure PostgreSQL is running on port 5433 (or port 5432 if using standard service) and the database `school_admission` is initialized.

```powershell
# If using the local development cluster:
& "C:\Program Files\PostgreSQL\18\bin\postgres.exe" -D "d:\Job\School Admission System\local_db" -p 5433
```

### Step 2: Start the Backend Web API

From the repository root:

```powershell
# Run the API server on http://localhost:5119
dotnet run --project backend/SchoolAdmission.Api/SchoolAdmission.Api.csproj --launch-profile http
```

*Alternatively, open `SchoolAdmission.sln` in Visual Studio, set `SchoolAdmission.Api` as Startup Project, and press **F5**.*

### Step 3: Start the React Frontend

Open a second terminal window in the `frontend` folder:

```powershell
cd frontend
npm install
npm run dev
```

The frontend will start at **`http://localhost:5173`**.

---

## 6. How to Run Migrations & Tests

### EF Core Migrations
```powershell
# From the repository root:
dotnet-ef database update --project backend/SchoolAdmission.Api/SchoolAdmission.Api.csproj
```

### Backend Automated Tests
```powershell
# Run all 20 xUnit integration tests against PostgreSQL
dotnet test SchoolAdmission.sln
```

### Frontend Production Build
```powershell
cd frontend
npm run build
```
*(Produces a production bundle in `frontend/dist` in ~250ms with 0 errors).*
