# School Admission System

A complete full-stack seat reservation and admission management system built for the **Assistant Software Engineer** recruitment assignment.

The application allows students to reserve temporary seats in specific classes/shifts using their 17-digit Birth Registration Number, handles seat expiration automatically, enforces strict concurrency control to prevent overbooking, and supports confirmation and cancellation of admissions.

---

## 🚀 Quick Start (Docker - Recommended)

The simplest and cleanest way to run the entire application (Database + Backend + Frontend) without installing .NET, Node.js, or PostgreSQL is with Docker.

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running.

### 1. Clone & Start
Open a terminal in the root project folder:

```bash
# Clone the repository
git clone https://github.com/Rifat-Bin-Alam/Assignment-for-School-Admissin-System.git
cd Assignment-for-School-Admissin-System

# Build and start all services
docker compose up --build
```

### 2. Access the Applications
Once the containers are built and started:
- **Frontend Web UI:** [http://localhost:3000](http://localhost:3000)
- **Backend Swagger API Docs:** [http://localhost:8080/swagger](http://localhost:8080/swagger)
- **Backend API Base:** [http://localhost:8080](http://localhost:8080)
- **PostgreSQL Database:** `localhost:5432`

### 3. Stop the Containers
```bash
docker compose down
```
*(All database data is stored in a persistent Docker volume and is preserved across restarts).*

---

## 💻 Running Locally Without Docker (Development Mode)

If you prefer running directly on your host machine using Visual Studio or the command line:

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 8 SDK)
- [Node.js 20+](https://nodejs.org/) & npm
- [PostgreSQL 16+](https://www.postgresql.org/)

### 1. Database Setup
Create a PostgreSQL database named `school_admission` (or let Entity Framework Core handle it).

Set your connection string in your environment or update `backend/SchoolAdmission.Api/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres"
}
```

### 2. Run Backend (.NET Web API)
```bash
# From repository root
cd backend/SchoolAdmission.Api
dotnet run
```
*Migrations are applied automatically at startup.*  
Backend will be available at: **`http://localhost:5119`** (or **`http://localhost:5119/swagger`**).

### 3. Run Frontend (React + Vite)
Open a second terminal:
```bash
cd frontend
npm install
npm run dev
```
Frontend will be available at: **`http://localhost:5173`**.

---

## 🧪 Running Automated Tests

The test suite includes 20 comprehensive xUnit tests covering concurrency safety, seat quota limits, hold expiration, simulated payments, idempotency, and cancellation.

To run the automated tests from the repository root:

```bash
dotnet test SchoolAdmission.sln
```

All 20 tests execute against an isolated test database and verify zero overbooking under concurrent hold attempts.

---

## 🛠️ Technology Stack

- **Backend:** C#, ASP.NET Core Web API (.NET 10 / .NET 8), Entity Framework Core (EF Core 10), Npgsql.
- **Frontend:** React 18, Vite, JavaScript, React Router, CSS.
- **Database:** PostgreSQL with row-level pessimistic locking (`SELECT ... FOR UPDATE`).
- **Testing:** xUnit, `Microsoft.AspNetCore.Mvc.Testing`.
- **Containerization & Deployment:** Docker, Docker Compose, Nginx.

---

## 📋 API Endpoints

All endpoints conform to the assignment specification:

| Method | Endpoint | Description | Headers |
|---|---|---|---|
| `POST` | `/classes` | Create a new class/shift | - |
| `GET` | `/classes` | List all classes with real-time seat counts | - |
| `GET` | `/classes/{classId}` | Get class details and seat availability | - |
| `POST` | `/classes/{classId}/holds` | Create temporary seat hold (10 min) | `X-Student-Id` |
| `GET` | `/holds/{holdId}` | Get hold details and countdown status | - |
| `POST` | `/holds/{holdId}/confirm` | Confirm admission (simulated payment) | `X-Student-Id` |
| `DELETE` | `/holds/{holdId}` | Cancel an active hold | `X-Student-Id` |
| `GET` | `/me/holds` | View all applications for current student | `X-Student-Id` |

---

## 📁 Project Folder Structure

```text
School Admission System/
│
├── docker-compose.yml               # Multi-container orchestration (PostgreSQL, Backend, Frontend)
├── SchoolAdmission.sln               # Visual Studio Solution File
├── .env.example                     # Environment variables template
├── .gitignore                       # Clean Git ignore rules (excludes bin, node_modules, secrets)
├── README.md                        # Setup and operational guide
├── PROJECT_GUIDE.md                 # Developer code map and architecture details
├── ASSUMPTIONS.md                   # Key architectural decisions & business assumptions
├── AI_USAGE.md                      # AI tool assistance and verification log
│
├── backend/
│   ├── Dockerfile                   # Multi-stage .NET SDK build & runtime image
│   ├── SchoolAdmission.Api/         # Controllers, Models, Data, Services, Migrations
│   └── SchoolAdmission.Tests/       # 20 Automated xUnit tests
│
└── frontend/
    ├── Dockerfile                   # Multi-stage Node.js build & Nginx production server
    ├── nginx.conf                   # Nginx reverse proxy and SPA routing
    ├── package.json                 # React dependencies and scripts
    └── src/
        ├── components/              # Reusable UI components (Navbar, SeatBadge)
        ├── pages/                   # AdminCreateClass, ClassList, Apply, MyApplications
        ├── services/                # API client with X-Student-Id header support
        └── styles/                  # Styling
```

---

## 📑 Core Business Rules Implemented

1. **Seat Integrity:** `heldSeats + confirmedSeats <= totalSeats` is strictly guaranteed at the database transaction level using pessimistic locking.
2. **Dual-Layer Expiration:** Holds expire after 10 minutes. Real-time queries treat expired holds as available seats immediately, while a background hosted service purges expired records periodically.
3. **Shift Separation:** Different shifts (e.g. Morning vs Day) are modeled as distinct classes with independent seat pools.
4. **Student Identification:** Identification is handled via the 17-digit Birth Registration Number supplied in the `X-Student-Id` header (no password/login required).
5. **Idempotent Confirmations:** Confirming an already confirmed hold returns success without duplicating confirmed seat counts.
