# gabMileage API Contract — Sprint 02: garage-vehicles

> **Sprint**: garage-vehicles  
> **Owner**: api-developer  
> **Last updated**: 2026-03-23  
> **Base**: `/api/v1/`

This document defines the HTTP contract between **gabMileage.Web** (TanStack Start) and the **gabMileage backend** (Gateway + Api) for Sprint 02.

---

## 0) Conventions

### 0.1 Auth
All `/api/v1/*` endpoints require authentication.

- Browser holds an **HTTP-only** `gab_session` cookie (WorkOS JWT).
- Gateway validates the JWT and forwards identity to the Api.
- Api derives the user context from the forwarded identity (JWT `sub` → `AuthContext.UserId`).
- The caller must **never** pass `userId` in request bodies.

> See `docs/api/foundation-auth.md` for the full auth flow and the internal `X-User-Id` header contract (Gateway → Api).

### 0.2 Content types
- Requests/responses: `application/json`
- Errors: `application/problem+json` (RFC 7807 ProblemDetails)

### 0.3 Time & date serialization
- Timestamps are ISO 8601 UTC strings (NodaTime `Instant`), e.g. `"2026-03-22T23:26:39Z"`.
- Dates are ISO 8601 date strings (NodaTime `LocalDate`), e.g. `"2026-03-22"`.

### 0.4 Common errors
- `401 Unauthorized` when missing/invalid session (ProblemDetails).
- `400 Bad Request` for validation errors (ProblemDetails + `errors` dictionary).
- `404 Not Found` when a resource does not exist **for the authenticated user** (includes cross-user access; never leak existence).

---

## 1) Data shapes

### 1.1 Vehicle (read model)
Used for the vehicle detail page.

```json
{
  "vehicleId": "uuid",
  "name": "string",
  "make": "string | null",
  "model": "string | null",
  "year": "integer | null",
  "photoUrl": "string | null",
  "photoThumbnailUrl": "string | null",
  "createdAt": "2026-03-22T23:26:39Z",
  "updatedAt": "2026-03-22T23:26:39Z"
}
```

Notes:
- `photoUrl` / `photoThumbnailUrl` are always `null` in this sprint (placeholders for Sprint 06).

### 1.2 GarageSummary
Used for the dashboard card grid.

```json
{
  "userId": "string",
  "vehicles": [
    {
      "vehicleId": "uuid",
      "name": "string",
      "make": "string | null",
      "model": "string | null",
      "year": "integer | null",
      "photoUrl": "string | null",
      "photoThumbnailUrl": "string | null",
      "createdAt": "2026-03-22T23:26:39Z"
    }
  ]
}
```

Notes:
- Vehicles are ordered **newest first** (descending `createdAt`).

---

## 2) Endpoints

### GET /api/v1/garage/summary
Fetch the authenticated user’s garage summary for the dashboard.

**Auth**: required  
**Owning vertical slice**: `Api.Garage`

**Response 200 OK**
```json
{
  "userId": "string",
  "vehicles": [
    {
      "vehicleId": "2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df",
      "name": "Civic",
      "make": "Honda",
      "model": "Civic",
      "year": 2018,
      "photoUrl": null,
      "photoThumbnailUrl": null,
      "createdAt": "2026-03-23T01:45:00Z"
    }
  ]
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)

---

### POST /api/v1/garage/vehicles
Add a vehicle to the authenticated user’s garage.

**Auth**: required  
**Owning vertical slice**: `Api.Garage`

**Request Body (AddVehicleCommand)**
```json
{
  "commandId": "uuid (required; client-generated idempotency key)",
  "name": "string (required; 1–50 chars after trim)",
  "make": "string | null (optional; ≤50 chars after trim)",
  "model": "string | null (optional; ≤50 chars after trim)",
  "year": "integer | null (optional; 1900–currentYear+1)"
}
```

Validation/normalization:
- Server trims `name`, `make`, `model`.
- Empty/whitespace-only `name` is invalid.

Idempotency:
- `commandId` is required.
- If the same authenticated user submits the same `commandId` again, the server performs a **no-op** and returns the previously created `vehicleId`.

**Response 201 Created (first time for this commandId)**
- `Location: /api/v1/garage/vehicles/{vehicleId}`
```json
{
  "vehicleId": "uuid"
}
```

**Response 200 OK (duplicate commandId; no-op)**
```json
{
  "vehicleId": "uuid"
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails):
  - `commandId` missing/empty
  - `name` missing/empty/too long
  - `make` too long
  - `model` too long
  - `year` out of range

---

### GET /api/v1/garage/vehicles/{vehicleId}
Fetch a single vehicle’s details for the vehicle detail page.

**Auth**: required  
**Owning vertical slice**: `Api.Garage`

**Route Params**:
- `vehicleId` (required): uuid

**Response 200 OK**
```json
{
  "vehicleId": "2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df",
  "name": "Civic",
  "make": "Honda",
  "model": "Civic",
  "year": 2018,
  "photoUrl": null,
  "photoThumbnailUrl": null,
  "createdAt": "2026-03-23T01:45:00Z",
  "updatedAt": "2026-03-23T01:45:00Z"
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid `vehicleId` route value
- `404 Not Found` (ProblemDetails): vehicle does not exist **or** belongs to a different user

---

### PUT /api/v1/garage/vehicles/{vehicleId}
Update a vehicle’s profile fields.

**Auth**: required  
**Owning vertical slice**: `Api.Garage`

**Route Params**:
- `vehicleId` (required): uuid

**Request Body**
```json
{
  "vehicleId": "uuid (optional; if provided must match route)",
  "name": "string (required; 1–50 chars after trim)",
  "make": "string | null (optional; ≤50 chars after trim)",
  "model": "string | null (optional; ≤50 chars after trim)",
  "year": "integer | null (optional; 1900–currentYear+1)"
}
```

Validation/normalization:
- Server trims `name`, `make`, `model`.
- If `vehicleId` is present in the body and does not match the URL `vehicleId`, the server returns `400 Bad Request`.

**Response 200 OK**
```json
{
  "vehicleId": "uuid"
}
```

Notes:
- The frontend already has the edited values, so the update response is intentionally minimal.
- The canonical updated resource can be fetched via `GET /api/v1/garage/vehicles/{vehicleId}`.

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails):
  - invalid `vehicleId` route value
  - `vehicleId` present in body but does not match the URL
  - invalid fields (same rules as POST)
- `404 Not Found` (ProblemDetails): vehicle does not exist **or** belongs to a different user

---

## 3) Wolverine messages (internal)

These messages are part of the internal contract for the **Api.Garage** slice. They are invoked by the HTTP endpoints above and stored via Marten event sourcing.

### Command: AddVehicleCommand
```csharp
public sealed record AddVehicleCommand(
    Guid CommandId,
    string Name,
    string? Make,
    string? Model,
    int? Year
);
```

### Command: UpdateVehicleCommand
```csharp
public sealed record UpdateVehicleCommand(
    Guid VehicleId,
    string Name,
    string? Make,
    string? Model,
    int? Year
);
```

### Event: VehicleAdded
```csharp
public sealed record VehicleAdded(
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    string Name,
    string? Make,
    string? Model,
    int? Year,
    Instant OccurredAt
);
```

### Event: VehicleUpdated
```csharp
public sealed record VehicleUpdated(
    Guid VehicleId,
    string UserId,
    string Name,
    string? Make,
    string? Model,
    int? Year,
    Instant OccurredAt
);
```

---

## 4) Server-sent events (SSE)

No SSE streams are introduced in Sprint 02.

---

## 5) Breadcrumbs (contract decisions)

### D-API-05: Vehicle endpoints live under `/api/v1/garage/*`
- **Who**: api-developer
- **What**: Use `/api/v1/garage/vehicles` and `/api/v1/garage/summary` for the garage loop.
- **Why**: The dashboard and detail UX are scoped to the user’s “garage” concept; matching paths keeps frontend routing intuitive and aligns with the domain model’s contract summary.
- **Alternatives considered**:
  - `/api/v1/vehicles` → rejected: would require a separate "garage" query anyway; garage is the primary bounded context.
- **Confidence**: High

### D-API-06: POST create is idempotent via `commandId`
- **Who**: api-developer
- **What**: `POST /api/v1/garage/vehicles` accepts `commandId` and returns:
  - `201 Created` on first processing
  - `200 OK` with the same `{ vehicleId }` on duplicate `commandId`
- **Why**: The sprint requires server-side idempotency for duplicate submissions (retries/double-clicks). Using status code differentiation preserves REST semantics while keeping the response shape stable.
- **Alternatives considered**:
  - Always `201 Created` (even on duplicates) → rejected: misleading for clients/telemetry.
  - Return `409 Conflict` for duplicates → rejected: duplicates are a success case, not a business violation.
- **Confidence**: High

### D-API-07: 404 for cross-user vehicle access
- **Who**: api-developer
- **What**: `GET`/`PUT` on a vehicle not owned by the caller returns `404 Not Found` (not `403`).
- **Why**: Prevents leaking whether a vehicle exists.
- **Alternatives considered**:
  - `403 Forbidden` → rejected: existence leak.
- **Confidence**: High

### D-API-08: Read models include photo fields as null placeholders
- **Who**: api-developer
- **What**: Include `photoUrl` and `photoThumbnailUrl` on both summary and detail payloads; always `null` in Sprint 02.
- **Why**: Establishes a stable shape and matches the domain model; adding later is safe, but including now avoids churn.
- **Alternatives considered**:
  - Omit fields until Sprint 06 → rejected: contract stability.
- **Confidence**: High

### D-API-09: PUT accepts `vehicleId` in route; body `vehicleId` is optional
- **Who**: api-developer
- **What**: `PUT /api/v1/garage/vehicles/{vehicleId}` uses the route as the authoritative identifier. Clients may optionally include `vehicleId` in the JSON body; if present it must match the route.
- **Why**: This keeps the REST shape clean (resource identity in the URL) while remaining compatible with a `UpdateVehicleCommand` message shape that includes `VehicleId`.
- **Alternatives considered**:
  - Require `vehicleId` in the body → rejected: redundant and easier to misuse.
  - Disallow `vehicleId` in the body entirely → rejected: makes it harder to reuse the same DTO/record shape across transport layers.
- **Confidence**: High

### D-API-10: Update response is minimal
- **Who**: api-developer
- **What**: `PUT /api/v1/garage/vehicles/{vehicleId}` returns `200 OK` with `{ vehicleId }` only.
- **Why**: The UI already has the edited values; returning the full vehicle would add extra query work without clear benefit in this sprint.
- **Alternatives considered**:
  - Return full `Vehicle` read model → deferred: can be added later as an extend-only response change by adding a `vehicle` field.
  - `204 No Content` → rejected: keeping a small body makes client logging/telemetry simpler.
- **Confidence**: Medium
