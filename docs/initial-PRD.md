# gabMileage — Product Requirements Document

## The Idea

gabMileage is a personal garage app that helps people stay on top of vehicle maintenance and fuel tracking — without it feeling like a chore. The experience should feel like keeping a journal with your car, not filling out a spreadsheet. Warm, personal, and fast to use.

## Target User

Someone who wants to stay on top of vehicle maintenance without it being a chore. They have low friction tolerance — if logging a fill-up takes more than 30 seconds, they stop doing it. They're not a car enthusiast, just a responsible owner who doesn't want to be surprised by a repair bill.

## Platform

Responsive web app (mobile-friendly design). Works in any browser, feels native on a phone.

## Key Design Principles

1. **Fast entry** — logging a fill-up or maintenance event should take under 30 seconds
2. **Warm tone** — vehicles have names and feel like companions, not assets
3. **No anxiety** — reminders should feel helpful, not nagging
4. **Simple first** — every feature earns its place; default to less

---

## User Flows

### Onboarding
- New user logs in via WorkOS and lands on an empty garage
- Immediately prompted to **Add a Vehicle**
- Minimum required: **name only** — that's enough to start tracking
- Optional fields (can be added later): make, model, year, photo

### Adding a Fuel Log
Fields:
- **Date** — defaults to today, fully editable (supports retroactive entry)
- **Odometer** — required; drives all mileage-based calculations
- **Any two of**: gallons, total cost, price per gallon — the third is calculated automatically
- **Optional**: partial fill-up flag, gas station, notes

### Defining a Maintenance Item
Each vehicle has user-defined maintenance items. Fields:
- **Name** (e.g., "Oil Change", "Tire Rotation")
- **Interval**: mileage, time (months), or both — "whichever comes first"
- **Warning threshold**: how early to surface a "due soon" alert (default: 500 miles / 2 weeks; user can override per item)

### Recording Maintenance Performed
- **Date** — defaults to today, editable
- **Odometer at time of service** — required
- **Cost** — optional
- **Notes** — optional
- Saving automatically **resets the interval clock** from the recorded date and odometer

### Dashboard
Two zones:
1. **Global "Needs Attention" strip** at the top — unified list of all overdue and due-soon items across all vehicles, sorted by urgency
2. **Garage card grid** below — one card per vehicle showing name, photo, and a due/overdue count badge

Overdue items are **flagged and visible** — present and clear, not alarming.

### Vehicle Detail View
- Vehicle profile header (name, photo, make, model, year — edit inline)
- **Fuel History tab** — reverse-chronological list of fill-ups with edit/delete per entry
- **Maintenance tab** — each defined item showing current status (ok / due soon / overdue), last performed date/mileage, and a history log of past service records

### Vehicle Lifecycle
- Vehicles can be **archived** — removed from the active garage but history is preserved
- Archived vehicles can be **restored** back to active
- Archived vehicles can also be **permanently deleted** (hard delete, unrecoverable)

### Editing & Deleting Logs
- Full **edit and delete** on any fuel log or maintenance record after the fact
- Event sourcing preserves the full audit trail internally

---

## Feature Scope (v1)

### In
- User accounts via WorkOS (login, logout, session management)
- Garage: add/edit/archive/restore/delete vehicles
- Vehicle profile: name (required), photo, make, model, year (all optional except name)
- Fuel logs: add/edit/delete fill-ups with calculated third field
- Maintenance items: define custom items per vehicle with mileage/time intervals and warning thresholds
- Maintenance records: log service events, auto-reset interval clock
- Dashboard: global attention strip + garage card grid
- Vehicle detail: fuel history tab + maintenance tab
- Mobile-friendly responsive UI

### Out (for now)
- Pre-loaded maintenance schedules
- Push / email notifications
- Sharing garages between users
- Native mobile app
- Trip tracking / GPS
- Expense reporting / analytics

---

## Tech Stack

| Concern | Technology |
|---|---|
| Orchestration | .NET Aspire |
| Backend API | ASP.NET Core (.NET) |
| Messaging / Command Handling | WolverineFx |
| Event Store / Persistence | MartenDB (on Postgres) |
| Database | PostgreSQL |
| Caching | Redis |
| Frontend | TanStack Start (React) |
| Authentication | WorkOS |
| Hosting | Azure Container Apps |
| Unit / Integration Tests | xUnit + Shouldly |
| E2E Tests | Playwright |
| Time | NodaTime |

---

## Architecture

**Modular monolith** to start. One .NET API project with domain modules (Vehicles, FuelLogs, Maintenance, Users). Not microservices — unnecessary complexity for v1.

### Domain Events (Marten Event Store)
- `VehicleAdded`, `VehicleUpdated`, `VehicleArchived`, `VehicleRestored`, `VehicleDeleted`
- `FuelLogRecorded`, `FuelLogUpdated`, `FuelLogDeleted`
- `MaintenanceItemDefined`, `MaintenanceItemUpdated`, `MaintenanceItemRemoved`
- `MaintenancePerformed`, `MaintenanceRecordUpdated`, `MaintenanceRecordDeleted`

Marten projections build read models for dashboard and vehicle views. Wolverine handles command dispatch and event processing.

### Solution Structure
```
gabMileage.sln
├── gabMileage.AppHost          # Aspire orchestrator
├── gabMileage.ServiceDefaults  # Shared Aspire service defaults
├── gabMileage.Api              # ASP.NET Core Web API (Marten + Wolverine)
├── gabMileage.Gateway          # ASP.NET Core YARP reverse proxy
├── gabMileage.Web              # TanStack Start frontend
└── tests/
    ├── gabMileage.Tests.Unit
    ├── gabMileage.Tests.Integration
    └── gabMileage.Tests.E2E
```

### Request Flow
```
Browser → TanStack Start → YARP Gateway → .NET API → Marten / Postgres
                                                    ↘ Redis (read cache)
```

### Authentication Flow
WorkOS handles login/registration. JWT tokens issued by WorkOS, validated at the YARP Gateway and forwarded to the API. Each request is scoped to the authenticated user's garage.

### Mileage Estimation
Linear interpolation between the two most recent odometer readings to estimate current mileage for maintenance due-date calculations.

### Photo Storage
Azure Blob Storage for vehicle photos.

---

## Hosting

- **Azure Container Apps** — each Aspire project deployed as a container
- **Azure Database for PostgreSQL** (Flexible Server)
- **Azure Cache for Redis**
- **Azure Blob Storage** for vehicle photos
- Aspire handles local orchestration in development; publishes to Azure Container Apps in production
