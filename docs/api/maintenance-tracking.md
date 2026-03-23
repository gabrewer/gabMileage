# gabMileage API Contract — Sprint 04: maintenance-tracking

> **Sprint**: maintenance-tracking  
> **Owner**: api-developer  
> **Last updated**: 2026-03-23  
> **Base**: `/api/v1/`

This document defines the HTTP contract between **gabMileage.Web** (TanStack Start) and the **gabMileage backend** (Gateway + Api) for Sprint 04.

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

> **Owning vertical slice for all endpoints in this document**: `Api.Garage.Maintenance`

### 1.1 MaintenanceTabResponse (Maintenance tab read model + query-time enrichment)
Returned by the Maintenance tab query endpoint.

```json
{
  "vehicleId": "uuid",
  "items": [
    {
      "itemId": "uuid",
      "name": "string",
      "mileageInterval": "integer | null",
      "timeIntervalMonths": "integer | null",
      "mileageWarning": "integer",
      "timeWarningDays": "integer",

      "status": "ok | dueSoon | overdue",
      "statusDetail": "string",
      "lastPerformedDate": "YYYY-MM-DD | null",
      "lastPerformedOdometer": "integer | null",

      "records": [
        {
          "recordId": "uuid",
          "itemId": "uuid",
          "date": "YYYY-MM-DD",
          "odometer": "integer",
          "cost": "number | null",
          "notes": "string | null",
          "createdAt": "ISO 8601 UTC"
        }
      ],
      "createdAt": "ISO 8601 UTC"
    }
  ],
  "lastOdometer": "integer | null"
}
```

Notes:
- `status` / `statusDetail` / `lastPerformed*` are computed at **query time** (not stored in the projection).
- `items[]` are sorted at query time: **overdue → dueSoon → ok**, then alphabetical by `name` within each group.
- `records[]` are sorted reverse-chronological: `date` desc, then `odometer` desc.
- `lastOdometer` is the **most recent odometer reading from any source** (fuel logs or maintenance records). The endpoint reconciles `MaintenanceTabData.LastOdometer` with `FuelLogList.LastOdometer`.

### 1.2 Maintenance status detail strings
The server returns human-readable strings per the domain rules, e.g.:
- `"Next due in ~2,300 mi"`
- `"Next due in 2 months"`
- `"Next due in ~2,300 mi or 2 months"`
- `"Due in ~450 mi"`
- `"Due in 1 week"`
- `"Overdue by ~800 mi"`
- `"Overdue by 3 weeks"`
- `"No service history — log your first service to start tracking"`
- `"Log a fill-up or service to start mileage tracking"`

---

## 2) Endpoints

### GET /api/v1/garage/vehicles/{vehicleId}/maintenance
Fetch the Maintenance tab data for a vehicle, including computed status and sorted items.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid

**Response 200 OK**
```json
{
  "vehicleId": "2a8d67b1-1c07-4c9b-a7f1-f9f3d3a2b2df",
  "items": [
    {
      "itemId": "f8a7f8b2-3a3f-4b50-93b0-5c8accc7bbf3",
      "name": "Oil Change",
      "mileageInterval": 5000,
      "timeIntervalMonths": 6,
      "mileageWarning": 500,
      "timeWarningDays": 14,
      "status": "dueSoon",
      "statusDetail": "Due in ~450 mi",
      "lastPerformedDate": "2026-01-15",
      "lastPerformedOdometer": 45230,
      "records": [
        {
          "recordId": "c1b9c4d3-1b8e-4cb7-96f6-06dfd3b5a0bd",
          "itemId": "f8a7f8b2-3a3f-4b50-93b0-5c8accc7bbf3",
          "date": "2026-01-15",
          "odometer": 45230,
          "cost": 69.99,
          "notes": "Full synthetic",
          "createdAt": "2026-01-15T18:00:00Z"
        }
      ],
      "createdAt": "2026-03-23T02:00:00Z"
    }
  ],
  "lastOdometer": 49600
}
```

Status calculation notes (Sprint 04 scope):
- `estimatedCurrentMileage` is the raw most-recent odometer reading from any source.
- Special case: items with **no history and no initial baseline** return `status=dueSoon` with detail `"No service history — log your first service to start tracking"` (never `overdue`).

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid `vehicleId` route value
- `404 Not Found` (ProblemDetails): vehicle does not exist **or** belongs to a different user

---

### POST /api/v1/garage/vehicles/{vehicleId}/maintenance/items
Define a new maintenance item for a vehicle.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid

**Request Body (DefineMaintenanceItemCommand)**
```json
{
  "commandId": "uuid (required; client-generated idempotency key)",
  "name": "string (required; 1–100 chars after trim)",

  "mileageInterval": "integer | null (optional; 1–999,999)",
  "timeIntervalMonths": "integer | null (optional; 1–120)",

  "mileageWarning": "integer (optional; default 500; must be < mileageInterval when mileageInterval set)",
  "timeWarningDays": "integer (optional; default 14; must be < timeIntervalMonths×30.44 when timeIntervalMonths set)",

  "initialLastPerformedDate": "YYYY-MM-DD | null (optional; must not be in the future)",
  "initialLastPerformedOdometer": "integer | null (optional; 1–9,999,999)"
}
```

Validation/normalization:
- Server trims `name`.
- At least one of `{ mileageInterval, timeIntervalMonths }` is required.
- `mileageWarning` / `timeWarningDays` default server-side when omitted.

Idempotency:
- `commandId` is required.
- If the same authenticated user submits the same `commandId` again, the server performs a **no-op** and returns the previously created `itemId`.

**Response 201 Created (first time for this commandId)**
- `Location: /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}`
```json
{
  "itemId": "uuid"
}
```

**Response 200 OK (duplicate commandId; no-op)**
```json
{
  "itemId": "uuid"
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails + `errors`):
  - `commandId`: missing/empty
  - `name`: `"Give this item a name"`
  - `intervals`: `"Set a mileage interval, time interval, or both"`
  - `mileageInterval`: `"Enter a valid mileage interval"`
  - `timeIntervalMonths`: `"Enter a valid time interval (1–120 months)"`
  - `mileageWarning`: `"Warning threshold must be less than the interval"`
  - `timeWarningDays`: `"Warning threshold must be less than the interval"`
  - `initialLastPerformedDate`: `"Date can't be in the future"`
  - `initialLastPerformedOdometer`: `"Enter a valid odometer reading"`
- `404 Not Found` (ProblemDetails): vehicle does not exist **or** belongs to a different user

---

### PUT /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}
Update a maintenance item’s definition.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid
- `itemId` (required): uuid

**Request Body (UpdateMaintenanceItemCommand)**
```json
{
  "vehicleId": "uuid (optional; if provided must match route)",
  "itemId": "uuid (optional; if provided must match route)",

  "name": "string (required; 1–100 chars after trim)",
  "mileageInterval": "integer | null (optional; 1–999,999)",
  "timeIntervalMonths": "integer | null (optional; 1–120)",

  "mileageWarning": "integer (required when mileageInterval is set; must be < mileageInterval)",
  "timeWarningDays": "integer (required when timeIntervalMonths is set; must be < timeIntervalMonths×30.44 in days)"
}
```

Notes:
- Initial baseline fields (`initialLastPerformed*`) are **not** editable; if provided, the server returns `400`.

**Response 200 OK**
```json
{
  "itemId": "uuid"
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid route ids; id mismatch; validation errors (same messages as POST, excluding initial state)
- `404 Not Found` (ProblemDetails): vehicle or item does not exist **or** belongs to a different user

---

### DELETE /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}
Remove a maintenance item and all its service history.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid
- `itemId` (required): uuid

**Request body**: none

**Response 204 No Content**

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid route ids
- `404 Not Found` (ProblemDetails): vehicle or item does not exist **or** belongs to a different user

---

### POST /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records
Log a new service record (maintenance performed) for an item.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid
- `itemId` (required): uuid

**Request Body (RecordMaintenanceCommand)**
```json
{
  "commandId": "uuid (required; client-generated idempotency key)",
  "date": "YYYY-MM-DD (required; not in the future)",
  "odometer": "integer (required; 1–9,999,999)",
  "cost": "number | null (optional; >= 0; <= 99,999.99; max 2 dp)",
  "notes": "string | null (optional; <= 500 chars)"
}
```

Idempotency:
- `commandId` is required.
- If the same authenticated user submits the same `commandId` again, the server performs a **no-op** and returns the previously created `recordId`.

**Response 201 Created (first time for this commandId)**
- `Location: /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}`
```json
{
  "recordId": "uuid"
}
```

**Response 200 OK (duplicate commandId; no-op)**
```json
{
  "recordId": "uuid"
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails + `errors`):
  - `commandId`: missing/empty
  - `date`: `"Date can't be in the future"`
  - `odometer`: `"Enter a valid odometer reading"`
  - `cost`: `"Enter a valid cost"`
  - `notes`: `"Notes are too long"`
- `404 Not Found` (ProblemDetails): vehicle or item does not exist **or** belongs to a different user

---

### PUT /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}
Update an existing service record.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid
- `itemId` (required): uuid
- `recordId` (required): uuid

**Request Body (UpdateMaintenanceRecordCommand)**
```json
{
  "vehicleId": "uuid (optional; if provided must match route)",
  "itemId": "uuid (optional; if provided must match route)",
  "recordId": "uuid (optional; if provided must match route)",

  "date": "YYYY-MM-DD (required; not in the future)",
  "odometer": "integer (required; 1–9,999,999)",
  "cost": "number | null (optional; >= 0; <= 99,999.99; max 2 dp)",
  "notes": "string | null (optional; <= 500 chars)"
}
```

**Response 200 OK**
```json
{
  "recordId": "uuid"
}
```

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid route ids; id mismatch; validation errors
- `404 Not Found` (ProblemDetails): vehicle, item, or record does not exist **or** belongs to a different user

---

### DELETE /api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}
Delete a service record.

**Auth**: required  
**Route Params**:
- `vehicleId` (required): uuid
- `itemId` (required): uuid
- `recordId` (required): uuid

**Request body**: none

**Response 204 No Content**

**Errors**:
- `401 Unauthorized` (ProblemDetails)
- `400 Bad Request` (ProblemDetails): invalid route ids
- `404 Not Found` (ProblemDetails): vehicle, item, or record does not exist **or** belongs to a different user

---

## 2.1 Compatibility alias (non-canonical)

To avoid drift with early Sprint 04 drafts, the backend **may** additionally map the following equivalent routes:

- `GET    /api/v1/vehicles/{vehicleId}/maintenance`
- `POST   /api/v1/vehicles/{vehicleId}/maintenance/items`
- `PUT    /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}`
- `DELETE /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}`
- `POST   /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}/records`
- `PUT    /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}`
- `DELETE /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}`

If implemented, these must be strict aliases of the canonical `/api/v1/garage/vehicles/...` routes (same auth, shapes, and semantics).

---

## 3) Wolverine messages (internal)

These messages are part of the internal contract for the `Api.Garage.Maintenance` slice. They are invoked by the HTTP endpoints above and persisted via Marten event sourcing on the `vehicle-{vehicleId}` stream.

### Commands

#### DefineMaintenanceItemCommand
```csharp
public sealed record DefineMaintenanceItemCommand(
    Guid CommandId,
    Guid VehicleId,
    string Name,
    int? MileageInterval,
    int? TimeIntervalMonths,
    int MileageWarning,
    int TimeWarningDays,
    LocalDate? InitialLastPerformedDate,
    int? InitialLastPerformedOdometer
);
```

#### UpdateMaintenanceItemCommand
```csharp
public sealed record UpdateMaintenanceItemCommand(
    Guid ItemId,
    Guid VehicleId,
    string Name,
    int? MileageInterval,
    int? TimeIntervalMonths,
    int MileageWarning,
    int TimeWarningDays
);
```

#### RemoveMaintenanceItemCommand
```csharp
public sealed record RemoveMaintenanceItemCommand(
    Guid ItemId,
    Guid VehicleId
);
```

#### RecordMaintenanceCommand
```csharp
public sealed record RecordMaintenanceCommand(
    Guid CommandId,
    Guid ItemId,
    Guid VehicleId,
    LocalDate Date,
    int Odometer,
    decimal? Cost,
    string? Notes
);
```

#### UpdateMaintenanceRecordCommand
```csharp
public sealed record UpdateMaintenanceRecordCommand(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId,
    LocalDate Date,
    int Odometer,
    decimal? Cost,
    string? Notes
);
```

#### DeleteMaintenanceRecordCommand
```csharp
public sealed record DeleteMaintenanceRecordCommand(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId
);
```

### Events

#### MaintenanceItemDefined
```csharp
public sealed record MaintenanceItemDefined(
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    string Name,
    int? MileageInterval,
    int? TimeIntervalMonths,
    int MileageWarning,
    int TimeWarningDays,
    LocalDate? InitialLastPerformedDate,
    int? InitialLastPerformedOdometer,
    Instant OccurredAt
);
```

#### MaintenanceItemUpdated
```csharp
public sealed record MaintenanceItemUpdated(
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    string Name,
    int? MileageInterval,
    int? TimeIntervalMonths,
    int MileageWarning,
    int TimeWarningDays,
    Instant OccurredAt
);
```

#### MaintenanceItemRemoved
```csharp
public sealed record MaintenanceItemRemoved(
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Instant OccurredAt
);
```

#### MaintenancePerformed
```csharp
public sealed record MaintenancePerformed(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    LocalDate Date,
    int Odometer,
    decimal? Cost,
    string? Notes,
    Instant OccurredAt
);
```

#### MaintenanceRecordUpdated
```csharp
public sealed record MaintenanceRecordUpdated(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    LocalDate Date,
    int Odometer,
    decimal? Cost,
    string? Notes,
    Instant OccurredAt
);
```

#### MaintenanceRecordDeleted
```csharp
public sealed record MaintenanceRecordDeleted(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Instant OccurredAt
);
```

---

## 4) Server-sent events (SSE)

No SSE streams are introduced in Sprint 04.

---

## 5) Breadcrumbs (contract decisions)

### D-API-MNT-01: Maintenance routes live under `/api/v1/garage/vehicles/{vehicleId}`
- **Who**: api-developer
- **What**: Canonical maintenance routes are nested under the Sprint 02 vehicle resource: `/api/v1/garage/vehicles/{vehicleId}/maintenance/...`.
- **Why**: Sprint 02 established vehicles under `/api/v1/garage/vehicles/*` and Sprint 03 followed with fuel logs as a sub-resource. Maintenance is another vehicle-scoped sub-resource and should follow the same convention.
- **Alternatives considered**:
  - `/api/v1/vehicles/{vehicleId}/maintenance/...` → rejected as canonical: inconsistent with existing garage-scoped routes; acceptable only as a potential compatibility alias if earlier drafts exist.
- **Confidence**: High

### D-API-MNT-02: GET returns query-time enriched `MaintenanceTabResponse`
- **Who**: api-developer
- **What**: `GET /maintenance` returns the enriched tab payload including `status`, `statusDetail`, and `lastPerformed*` per item.
- **Why**: The UI needs urgency sorting, color dots, and human-readable status text. Domain explicitly requires status computed at query time because it changes with time/mileage.
- **Alternatives considered**:
  - Store status in the projection → rejected: would go stale without events.
- **Confidence**: High

### D-API-MNT-03: POST create endpoints are idempotent via `commandId`
- **Who**: api-developer
- **What**: `POST /maintenance/items` and `POST /maintenance/items/{itemId}/records` require `commandId` and return `201` on first use, `200` on duplicates.
- **Why**: Matches Sprint 02/03 idempotent create behavior and supports retries/double-clicks without creating duplicate items/records.
- **Alternatives considered**:
  - `409 Conflict` on duplicate commandId → rejected: duplicate submits are a success case.
- **Confidence**: High

### D-API-MNT-04: Warning thresholds default on create; update requires explicit thresholds
- **Who**: api-developer
- **What**:
  - On **create** (`POST`), `mileageWarning` and `timeWarningDays` are optional; when omitted, the server applies defaults (500 miles / 14 days) when their corresponding interval is set.
  - On **update** (`PUT`), the client must send the current warning thresholds for any configured interval.
- **Why**: The create UX starts with sensible defaults (collapsed “When to warn me”). For updates, requiring explicit thresholds avoids accidental resets to defaults if a client omits fields.
- **Alternatives considered**:
  - Make thresholds optional on update and treat as PATCH semantics → rejected: would introduce partial-update behavior on a `PUT` endpoint.
- **Confidence**: Medium

### D-API-MNT-05: 404 (not 403) for cross-user access to vehicle/item/record
- **Who**: api-developer
- **What**: All not-owned resources return `404 Not Found`.
- **Why**: Prevents leaking resource existence.
- **Alternatives considered**:
  - `403 Forbidden` → rejected: existence leak.
- **Confidence**: High
