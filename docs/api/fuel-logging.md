# gabMileage API Contract — Sprint 03: fuel-logging

> **Sprint**: fuel-logging  
> **Owner**: api-developer  
> **Last updated**: 2026-03-23  
> **Base**: `/api/v1/`

This document defines the HTTP contract between **gabMileage.Web** (TanStack Start) and the **gabMileage backend** (Gateway + Api) for Sprint 03.

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

### 0.4 Decimal precision & rounding
Fuel values are JSON numbers.

Server normalization rules (mirrors `FuelFieldCalculator` in the domain model):
- `gallons`: round/calculated to **3** decimal places.
- `totalCost`: round/calculated to **2** decimal places.
- `pricePerGallon`: round/calculated to **3** decimal places.

### 0.5 Common errors
- `401 Unauthorized` when missing/invalid session (ProblemDetails).
- `400 Bad Request` for validation errors (ProblemDetails + `errors` dictionary).
- `404 Not Found` when a vehicle/fuel log does not exist **for the authenticated user** (includes cross-user access; never leak existence).

---

## 1) Data shapes

### 1.1 FuelLog (read model entry)

```json
{
  "fuelLogId": "uuid",
  "date": "YYYY-MM-DD (LocalDate)",
  "odometer": "integer (miles)",
  "gallons": "number (decimal; > 0; max 999.999; max 3 dp)",
  "totalCost": "number (decimal; >= 0; max 9999.99; max 2 dp)",
  "pricePerGallon": "number (decimal; >= 0; max 99.999; max 3 dp)",
  "calculatedField": "gallons | totalCost | pricePerGallon | null",
  "isPartialFillUp": "boolean",
  "notes": "string | null",
  "mpg": "number | null (server-calculated; 1 dp)",
  "createdAt": "ISO 8601 UTC (Instant)"
}
```

Notes:
- `gallons`, `totalCost`, and `pricePerGallon` are **always present** in the read model (server ensures all three exist; see domain Decision D-06).
- `calculatedField` preserves which field was auto-calculated for edit UX.
- `mpg` is computed server-side and may be `null` (suppressed) per MPG rules.

### 1.2 FuelLogList
Returned for the Fuel tab.

```json
{
  "vehicleId": "uuid",
  "logs": [ /* FuelLog[] */ ],
  "lastOdometer": "integer | null"
}
```

Sorting:
- `logs[]` are sorted by `date` **descending**, then `odometer` **descending** for same-date ties.

`lastOdometer`:
- Most recent odometer reading in display order (first element’s `odometer`), or `null` if no logs.

### 1.3 FuelLogCreateResponse

```json
{
  "fuelLogId": "uuid",
  "odometerWarning": "string | null"
}
```

`odometerWarning`:
- Optional warning string when the entered odometer is lower than the most recent prior reading.
- This is **not** an error; the fill-up is still saved.

---

## 2) Endpoints

> **Owning vertical slice for all endpoints in this document**: `Api.Garage.FuelLogs`

### GET /api/v1/garage/vehicles/{vehicleId}/fuel-logs
Fetch the fuel log list for a vehicle, including MPG calculations and `lastOdometer`.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid

**Response 200 OK**
```json
{
  "vehicleId": "2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df",
  "logs": [
    {
      "fuelLogId": "b5c4c1a6-9cc0-48c5-8a21-45b1b4f6c4b6",
      "date": "2026-03-15",
      "odometer": 45230,
      "gallons": 12.5,
      "totalCost": 42.5,
      "pricePerGallon": 3.4,
      "calculatedField": "pricePerGallon",
      "isPartialFillUp": false,
      "notes": "Regular unleaded",
      "mpg": 28.4,
      "createdAt": "2026-03-15T14:30:00Z"
    }
  ],
  "lastOdometer": 45230
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid `vehicleId` route value
- `404 Not Found` (ProblemDetails): vehicle does not exist **or** belongs to a different user

---

### POST /api/v1/garage/vehicles/{vehicleId}/fuel-logs
Record a new fill-up for a vehicle.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid

**Request Body**
```json
{
  "commandId": "uuid (required; client-generated idempotency key)",
  "date": "YYYY-MM-DD (required; not in the future)",
  "odometer": "integer (required; 1–9,999,999)",

  "gallons": "number | null",
  "totalCost": "number | null",
  "pricePerGallon": "number | null",
  "calculatedField": "gallons | totalCost | pricePerGallon | null",

  "isPartialFillUp": "boolean (required)",
  "notes": "string | null"
}
```

Validation/normalization:
- `commandId` required and must be a non-empty uuid.
- `date` required; must not be in the future.
- `odometer` required; positive int ≤ 9,999,999.
- At least **two** of `{ gallons, totalCost, pricePerGallon }` must be provided.
- Field constraints:
  - `gallons` (if provided): **> 0**, max `999.999`, max 3 decimal places.
  - `totalCost` (if provided): **≥ 0**, max `9999.99`, max 2 decimal places.
  - `pricePerGallon` (if provided): **≥ 0**, max `99.999`, max 3 decimal places.
- `notes` (if provided): trimmed; max 500 chars.

Server calculation behavior:
- If exactly two of the three fuel fields are provided, the server calculates the third and sets `calculatedField` accordingly.
- If all three are provided and `calculatedField` is `null`, the server may recalculate `pricePerGallon` from `(totalCost / gallons)` if inconsistent (per sprint rule: prioritize gallons + totalCost).

Idempotency:
- If the same authenticated user submits the same `commandId` again, the server performs a **no-op** and returns the previously created `fuelLogId`.

**Response 201 Created (first time for this commandId)**
- `Location: /api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}`
```json
{
  "fuelLogId": "b5c4c1a6-9cc0-48c5-8a21-45b1b4f6c4b6",
  "odometerWarning": null
}
```

**Response 200 OK (duplicate commandId; no-op)**
```json
{
  "fuelLogId": "b5c4c1a6-9cc0-48c5-8a21-45b1b4f6c4b6",
  "odometerWarning": null
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): validation errors
- `404 Not Found` (ProblemDetails): vehicle does not exist **or** belongs to a different user

---

### PUT /api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}
Update an existing fill-up.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid
- `fuelLogId` (required): uuid

**Request Body**
```json
{
  "vehicleId": "uuid (optional; if provided must match route)",
  "fuelLogId": "uuid (optional; if provided must match route)",

  "date": "YYYY-MM-DD (required; not in the future)",
  "odometer": "integer (required; 1–9,999,999)",

  "gallons": "number | null",
  "totalCost": "number | null",
  "pricePerGallon": "number | null",
  "calculatedField": "gallons | totalCost | pricePerGallon | null",

  "isPartialFillUp": "boolean (required)",
  "notes": "string | null"
}
```

Validation/normalization:
- Same rules as POST (except no `commandId`).
- If `vehicleId` or `fuelLogId` are present in the body and don’t match the route values, the server returns `400 Bad Request`.

**Response 200 OK**
```json
{
  "fuelLogId": "uuid"
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid route ids; id mismatch; validation errors
- `404 Not Found` (ProblemDetails): vehicle or fuel log does not exist **or** belongs to a different user

---

### DELETE /api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}
Delete a fill-up.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid
- `fuelLogId` (required): uuid

**Request body**: none

**Response 204 No Content**

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid route ids
- `404 Not Found` (ProblemDetails): vehicle or fuel log does not exist **or** belongs to a different user

---

## 3) Compatibility alias (non-canonical)

To avoid drift with early Sprint 03 drafts, the backend **may** additionally map the following equivalent routes:

- `GET  /api/v1/vehicles/{vehicleId}/fuel-logs`
- `POST /api/v1/vehicles/{vehicleId}/fuel-logs`
- `PUT  /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}`
- `DELETE /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}`

If implemented, these must be strict aliases of the canonical `/api/v1/garage/vehicles/...` routes (same auth, shapes, and semantics).

---

## 4) Wolverine messages (internal)

These messages are part of the internal contract for the `Api.Garage.FuelLogs` slice. They are invoked by the HTTP endpoints above and persisted via Marten event sourcing.

### Commands

#### RecordFuelLogCommand
```csharp
public sealed record RecordFuelLogCommand(
    Guid CommandId,
    Guid VehicleId,
    LocalDate Date,
    int Odometer,
    decimal? Gallons,
    decimal? TotalCost,
    decimal? PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes
);
```

#### UpdateFuelLogCommand
```csharp
public sealed record UpdateFuelLogCommand(
    Guid FuelLogId,
    Guid VehicleId,
    LocalDate Date,
    int Odometer,
    decimal? Gallons,
    decimal? TotalCost,
    decimal? PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes
);
```

#### DeleteFuelLogCommand
```csharp
public sealed record DeleteFuelLogCommand(
    Guid FuelLogId,
    Guid VehicleId
);
```

### Events

#### FuelLogRecorded
```csharp
public sealed record FuelLogRecorded(
    Guid FuelLogId,
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    LocalDate Date,
    int Odometer,
    decimal Gallons,
    decimal TotalCost,
    decimal PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes,
    Instant OccurredAt
);
```

#### FuelLogUpdated
```csharp
public sealed record FuelLogUpdated(
    Guid FuelLogId,
    Guid VehicleId,
    string UserId,
    LocalDate Date,
    int Odometer,
    decimal Gallons,
    decimal TotalCost,
    decimal PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes,
    Instant OccurredAt
);
```

#### FuelLogDeleted
```csharp
public sealed record FuelLogDeleted(
    Guid FuelLogId,
    Guid VehicleId,
    string UserId,
    Instant OccurredAt
);
```

---

## 5) Server-sent events (SSE)

No SSE streams are introduced in Sprint 03.

---

## 6) Breadcrumbs (contract decisions)

### D-API-FUEL-01: Fuel log endpoints live under `/api/v1/garage/vehicles/{vehicleId}`
- **Who**: api-developer
- **What**: Canonical fuel log routes are `/api/v1/garage/vehicles/{vehicleId}/fuel-logs` (and sub-resource routes).
- **Why**: Sprint 02 established the vehicle resource under `/api/v1/garage/vehicles/*`; keeping fuel logs as a sub-resource maintains consistency and reduces frontend routing ambiguity.
- **Alternatives considered**:
  - `/api/v1/vehicles/{vehicleId}/fuel-logs` → accepted as optional alias only (see Compatibility alias section) to avoid drift with early drafts.
- **Confidence**: High

### D-API-FUEL-02: POST create is idempotent via `commandId` and returns 201 vs 200
- **Who**: api-developer
- **What**: `POST` returns:
  - `201 Created` on first processing of `commandId`
  - `200 OK` with the same response shape on duplicate `commandId` (no-op)
- **Why**: Matches Sprint 02 idempotent create behavior and preserves REST semantics while supporting retries/double-clicks.
- **Alternatives considered**:
  - Always `201 Created` → rejected: misleading for telemetry.
  - `409 Conflict` on duplicates → rejected: duplicates are a success case.
- **Confidence**: High

### D-API-FUEL-03: Server may return an `odometerWarning` on successful create
- **Who**: api-developer
- **What**: Create response includes optional `odometerWarning` string.
- **Why**: Sprint brief requires lower-than-last odometer to be a warning, not an error; returning it enables the UI to inform the user without blocking save.
- **Alternatives considered**:
  - Reject with 400 → rejected: conflicts with sprint requirement.
  - Save silently with no warning → rejected: loses useful feedback.
- **Confidence**: Medium
