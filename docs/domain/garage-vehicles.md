# Domain Model — Sprint 02: Garage & Vehicles

> **Sprint**: garage-vehicles
> **Modeler**: domain-modeler
> **Date**: 2026-03-23
> **Status**: 🔒 Locked — event catalog finalized before builders begin
> **Depends on**: Sprint 01 (foundation-auth) — `User` aggregate, `AuthContext`, string-keyed Marten streams

---

## Overview

Sprint 02 introduces the **Vehicle** aggregate — the first real domain entity in gabMileage. Users can add vehicles to their garage and edit vehicle profiles. The sprint establishes two new domain events (`VehicleAdded`, `VehicleUpdated`), two commands, two projections (single-stream `VehicleProjection` + multi-stream `GarageSummaryProjection`), and the read models that power the dashboard card grid and vehicle detail page.

The `GarageSummary` read model — introduced as an empty contract in Sprint 01 — is now backed by a real multi-stream projection. The `GarageSummaryVehicle` placeholder is replaced with full vehicle fields.

---

## 1. Aggregates & Entities

### Vehicle

The `Vehicle` aggregate represents a named vehicle in a user's garage. It is the primary domain entity for tracking fuel and maintenance data in later sprints.

| Property | Type | Description |
|---|---|---|
| **Id** | `Guid` | Vehicle identity. Also used in stream key `vehicle-{id}`. |
| **UserId** | `string` | WorkOS user ID. Owner of this vehicle. Set at creation, immutable. |
| **Name** | `string` | User-chosen display name (1–50 chars, trimmed). Required. |
| **Make** | `string?` | Vehicle make (e.g., "Honda"). Optional, ≤50 chars. |
| **Model** | `string?` | Vehicle model (e.g., "Civic"). Optional, ≤50 chars. |
| **Year** | `int?` | Model year (1900–currentYear+1). Optional. |
| **CreatedAt** | `Instant` | When the vehicle was added. Set from `VehicleAdded.OccurredAt`. |
| **UpdatedAt** | `Instant` | When the vehicle was last modified. Updated on every event. |
| **CommandId** | `Guid` | The `commandId` from the `AddVehicleCommand` that created this vehicle. Used for idempotency. |

**Stream ID**: `vehicle-{vehicleId}` (string-keyed stream — Guid formatted as string, consistent with Sprint 01's `StreamIdentity.AsString`)

**Invariants**:

1. `Name` must be non-empty after trimming and ≤50 characters.
2. `Make`, if provided, must be ≤50 characters.
3. `Model`, if provided, must be ≤50 characters.
4. `Year`, if provided, must be in range `[1900, currentYear + 1]`.
5. `UserId` is immutable after creation — a vehicle cannot change owners.
6. Two vehicles belonging to the same user may have the same name (names are not unique keys).
7. `CommandId` is unique across all vehicles for a given user. Duplicate `commandId` submissions are no-ops.

**Aggregate shape** (per established Marten conventions):

```csharp
public sealed class Vehicle
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Make { get; private set; }
    public string? Model { get; private set; }
    public int? Year { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CommandId { get; private set; }

    public void Apply(VehicleAdded e)
    {
        Id = e.VehicleId;
        UserId = e.UserId;
        Name = e.Name;
        Make = e.Make;
        Model = e.Model;
        Year = e.Year;
        CreatedAt = e.OccurredAt;
        UpdatedAt = e.OccurredAt;
        CommandId = e.CommandId;
    }

    public void Apply(VehicleUpdated e)
    {
        Name = e.Name;
        Make = e.Make;
        Model = e.Model;
        Year = e.Year;
        UpdatedAt = e.OccurredAt;
    }
}
```

---

## 2. Value Objects

This sprint introduces no new value objects. All fields on the Vehicle aggregate use primitive types or NodaTime types directly.

**Rationale**: The Vehicle aggregate in Sprint 02 carries only simple string/int/nullable fields with length constraints. Introducing wrapper types like `VehicleName` or `ModelYear` would add indirection without preventing bugs that simple validation doesn't already catch. Value objects for constrained domain types (`Odometer`, `FuelAmount`, `MaintenanceInterval`) will emerge in Sprints 03–04 where the types carry calculation logic and domain invariants beyond simple length/range checks.

---

## 3. Event Catalog

### VehicleAdded

Records that a user has added a new vehicle to their garage.

```
Event:       VehicleAdded
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (Guid formatted as string)

Payload:
  - VehicleId  : Guid      — New vehicle identity. Generated server-side.
  - UserId     : string    — WorkOS user ID (from AuthContext). Owner of the vehicle.
  - CommandId  : Guid      — Client-generated idempotency key.
  - Name       : string    — Vehicle display name. 1–50 chars, trimmed. Required.
  - Make       : string?   — Vehicle make. ≤50 chars. Nullable.
  - Model      : string?   — Vehicle model. ≤50 chars. Nullable.
  - Year       : int?      — Model year. 1900–(currentYear+1). Nullable.
  - OccurredAt : Instant   — NodaTime timestamp of when the vehicle was added.

Invariants:
  - VehicleId must be a non-empty Guid (generated server-side via Guid.NewGuid())
  - UserId must be non-empty
  - CommandId must be a non-empty Guid (generated client-side)
  - Name must be non-empty after trimming, ≤50 chars
  - Make (if present) ≤50 chars
  - Model (if present) ≤50 chars
  - Year (if present) in [1900, currentYear + 1]
  - CommandId must not match any previously processed AddVehicleCommand for this user

Trigger:
  - Frontend POST to add-vehicle endpoint with AddVehicleCommand payload
```

**C# record definition**:

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

### VehicleUpdated

Records that a vehicle's profile fields have been modified.

```
Event:       VehicleUpdated
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (Guid formatted as string)

Payload:
  - VehicleId  : Guid      — The vehicle being updated.
  - UserId     : string    — WorkOS user ID (from AuthContext). Included for ownership scoping.
  - Name       : string    — Updated display name. 1–50 chars, trimmed. Required.
  - Make       : string?   — Updated make. ≤50 chars. Nullable.
  - Model      : string?   — Updated model. ≤50 chars. Nullable.
  - Year       : int?      — Updated year. 1900–(currentYear+1). Nullable.
  - OccurredAt : Instant   — NodaTime timestamp of when the update occurred.

Invariants:
  - VehicleId must match an existing vehicle stream
  - UserId must be non-empty
  - The vehicle must belong to the authenticated user (ownership check enforced at handler level)
  - Name must be non-empty after trimming, ≤50 chars
  - Make (if present) ≤50 chars
  - Model (if present) ≤50 chars
  - Year (if present) in [1900, currentYear + 1]

Trigger:
  - Frontend PUT to update-vehicle endpoint with UpdateVehicleCommand payload

Note:
  - Ownership is immutable: handlers must verify ownership before emitting, and projections/aggregates must not overwrite the stored owner on updates.
  - UserId is carried on updates to support ownership scoping in downstream projections and queries.
```

**C# record definition**:

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

## 4. Command Catalog

### AddVehicleCommand

Adds a new vehicle to the authenticated user's garage. Idempotent via `commandId` — if the same `commandId` has been processed, returns the existing `vehicleId` without emitting a new event.

```
Command:     AddVehicleCommand
Handler:     AddVehicleHandler (static class, Wolverine command handler)
Emits:       VehicleAdded (only if commandId has not been processed)
Stream:      vehicle-{newVehicleId} (new stream)

Payload:
  - CommandId : Guid      — Client-generated UUID for idempotency
  - Name      : string    — Vehicle display name (required, 1–50 chars trimmed)
  - Make      : string?   — Vehicle make (optional, ≤50 chars)
  - Model     : string?   — Vehicle model (optional, ≤50 chars)
  - Year      : int?      — Model year (optional, 1900–currentYear+1)

Validation:
  - CommandId required, non-empty Guid
  - Name required, non-empty after trimming, ≤50 chars
  - Make (if present) ≤50 chars
  - Model (if present) ≤50 chars
  - Year (if present) in [1900, currentYear + 1]

Idempotency:
  - Handler queries for an existing ProcessedAddVehicle document with the same CommandId
  - If found, returns the previously created VehicleId (no event emitted, no error)
  - If not found, generates a new VehicleId, emits VehicleAdded, and stores a
    ProcessedAddVehicle { CommandId, VehicleId } document for future lookups

Caller:
  - Frontend via POST /api/v1/garage/vehicles
```

**C# record definition**:

```csharp
public sealed record AddVehicleCommand(
    Guid CommandId,
    string Name,
    string? Make,
    string? Model,
    int? Year
);
```

**Handler sketch** (per Marten + Wolverine conventions):

```csharp
public static class AddVehicleHandler
{
    public static async Task<AddVehicleResult> Handle(
        AddVehicleCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // Idempotency: check if this commandId was already processed
        var existing = await session.LoadAsync<ProcessedAddVehicle>(command.CommandId, ct);
        if (existing is not null)
            return new AddVehicleResult(existing.VehicleId);

        // Validation
        var trimmedName = command.Name.Trim();
        // ... validate Name, Make, Model, Year ...

        var vehicleId = Guid.NewGuid();
        var now = clock.GetCurrentInstant();

        var @event = new VehicleAdded(
            vehicleId,
            auth.UserId,
            command.CommandId,
            trimmedName,
            command.Make?.Trim(),
            command.Model?.Trim(),
            command.Year,
            now);

        session.Events.StartStream<Vehicle>($"vehicle-{vehicleId}", @event);

        // Store idempotency record alongside the event
        session.Store(new ProcessedAddVehicle
        {
            Id = command.CommandId,
            VehicleId = vehicleId
        });

        await session.SaveChangesAsync(ct);
        return new AddVehicleResult(vehicleId);
    }
}
```

### UpdateVehicleCommand

Updates an existing vehicle's profile fields. The vehicle must exist and belong to the authenticated user.

```
Command:     UpdateVehicleCommand
Handler:     UpdateVehicleHandler (static class, Wolverine command handler)
Emits:       VehicleUpdated
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - VehicleId : Guid      — The vehicle to update (from URL route)
  - Name      : string    — Updated display name (required, 1–50 chars trimmed)
  - Make      : string?   — Updated make (optional, ≤50 chars)
  - Model     : string?   — Updated model (optional, ≤50 chars)
  - Year      : int?      — Updated year (optional, 1900–currentYear+1)

Validation:
  - VehicleId required, must reference an existing vehicle
  - Name required, non-empty after trimming, ≤50 chars
  - Make (if present) ≤50 chars
  - Model (if present) ≤50 chars
  - Year (if present) in [1900, currentYear + 1]

Access control:
  - Handler loads the Vehicle read model and checks UserId == AuthContext.UserId
  - If vehicle not found OR belongs to a different user → 404 (not 403)

Caller:
  - Frontend via PUT /api/v1/garage/vehicles/{vehicleId}
```

**C# record definition**:

```csharp
public sealed record UpdateVehicleCommand(
    Guid VehicleId,
    string Name,
    string? Make,
    string? Model,
    int? Year
);
```

**Handler sketch**:

```csharp
public static class UpdateVehicleHandler
{
    public static async Task<IResult> Handle(
        UpdateVehicleCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // Load projected read model to check ownership
        var streamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(streamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            return Results.NotFound();

        // Validation
        var trimmedName = command.Name.Trim();
        // ... validate Name, Make, Model, Year ...

        var now = clock.GetCurrentInstant();
        var @event = new VehicleUpdated(
            command.VehicleId,
            auth.UserId,
            trimmedName,
            command.Make?.Trim(),
            command.Model?.Trim(),
            command.Year,
            now);

        session.Events.Append(streamId, @event);
        await session.SaveChangesAsync(ct);

        return Results.Ok();
    }
}
```

---

## 5. Projections

### VehicleProjection (Single-Stream)

Builds the `VehicleReadModel` from events on a single vehicle stream. Uses Marten's `SingleStreamProjection` with `Inline` lifecycle for immediate read-after-write consistency.

```
Projection:    VehicleProjection
Type:          SingleStreamProjection<VehicleReadModel, Guid>
Lifecycle:     Inline
Source Events: VehicleAdded, VehicleUpdated

Read Model:    VehicleReadModel
```

**Read model shape**:

```csharp
public sealed class VehicleReadModel
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? PhotoUrl { get; set; }             // null until Sprint 06
    public string? PhotoThumbnailUrl { get; set; }    // null until Sprint 06
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
}
```

**Projection implementation**:

```csharp
public sealed class VehicleProjection : SingleStreamProjection<VehicleReadModel, Guid>
{
    public VehicleProjection()
    {
        Lifecycle = ProjectionLifecycle.Inline;

        CreateEvent<VehicleAdded>(e => new VehicleReadModel
        {
            Id = e.VehicleId,
            UserId = e.UserId,
            Name = e.Name,
            Make = e.Make,
            Model = e.Model,
            Year = e.Year,
            PhotoUrl = null,
            PhotoThumbnailUrl = null,
            CreatedAt = e.OccurredAt,
            UpdatedAt = e.OccurredAt,
        });

        ProjectEvent<VehicleUpdated>((rm, e) =>
        {
            rm.Name = e.Name;
            rm.Make = e.Make;
            rm.Model = e.Model;
            rm.Year = e.Year;
            rm.UpdatedAt = e.OccurredAt;
            return rm;
        });
    }
}
```

**Notes**:
- The `VehicleReadModel` document is keyed by `Guid` (the `VehicleId`).
- Because the Marten stream key is `"vehicle-{vehicleId}"` (a string), the projection must map the stream identity to the document's `Guid Id`. Marten handles this via the `VehicleAdded.VehicleId` → `Id` assignment in `CreateEvent`.
- `PhotoUrl` and `PhotoThumbnailUrl` are included in the read model for API contract stability but are always `null` until Sprint 06.

### GarageSummaryProjection (Multi-Stream)

Builds the `GarageSummary` read model by aggregating vehicle events across all vehicles for a given user. Keyed by `UserId` (string). Uses Marten's `MultiStreamProjection` with `Inline` lifecycle.

```
Projection:    GarageSummaryProjection
Type:          MultiStreamProjection<GarageSummary, string>
Lifecycle:     Inline
Source Events: VehicleAdded, VehicleUpdated

Read Model:    GarageSummary (keyed by UserId)
```

**Read model shape** (replaces Sprint 01 placeholder):

```csharp
public sealed class GarageSummary
{
    public string Id { get; set; } = string.Empty;  // UserId — Marten document key
    public IList<GarageSummaryVehicle> Vehicles { get; set; } = [];
}

public sealed class GarageSummaryVehicle
{
    public Guid VehicleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? PhotoUrl { get; set; }             // null until Sprint 06
    public string? PhotoThumbnailUrl { get; set; }    // null until Sprint 06
    public Instant CreatedAt { get; set; }
}
```

**Projection implementation**:

```csharp
public sealed class GarageSummaryProjection : MultiStreamProjection<GarageSummary, string>
{
    public GarageSummaryProjection()
    {
        Lifecycle = ProjectionLifecycle.Inline;

        // Route events to the user's GarageSummary document
        Identity<VehicleAdded>(e => e.UserId);
        Identity<VehicleUpdated>(e =>
        {
            // VehicleUpdated doesn't carry UserId — we need the aggregate
            // to resolve the user. Use ViewProjection grouping instead.
            // See Decision D-07 for how this is resolved.
            throw new NotImplementedException("Resolved via custom grouper — see D-07");
        });

        ProjectEvent<VehicleAdded>((summary, e) =>
        {
            summary.Id = e.UserId;
            // Insert at position 0 for newest-first ordering
            summary.Vehicles.Insert(0, new GarageSummaryVehicle
            {
                VehicleId = e.VehicleId,
                Name = e.Name,
                Make = e.Make,
                Model = e.Model,
                Year = e.Year,
                PhotoUrl = null,
                PhotoThumbnailUrl = null,
                CreatedAt = e.OccurredAt,
            });
            return summary;
        });

        ProjectEvent<VehicleUpdated>((summary, e) =>
        {
            var vehicle = summary.Vehicles.FirstOrDefault(v => v.VehicleId == e.VehicleId);
            if (vehicle is not null)
            {
                vehicle.Name = e.Name;
                vehicle.Make = e.Make;
                vehicle.Model = e.Model;
                vehicle.Year = e.Year;
            }
            return summary;
        });
    }
}
```

**Notes on VehicleUpdated routing** (see Decision D-07):
- `VehicleUpdated` does not carry `UserId` because ownership is immutable.
- The projection needs to route `VehicleUpdated` to the correct `GarageSummary` document (keyed by `UserId`).
- **Implementation approach**: Use Marten's `CustomGrouping` or a `ViewProjection` with a custom event slicer that looks up the `VehicleReadModel` to resolve the `UserId` from the `VehicleId`. Alternatively, include `UserId` in `VehicleUpdated` as a denormalized field — see Decision D-07.

### ProcessedAddVehicle (Marten Document — Not a Projection)

A simple Marten document for server-side idempotency of the `AddVehicleCommand`. Not event-sourced — stored directly via `session.Store()`.

```csharp
public sealed class ProcessedAddVehicle
{
    public Guid Id { get; set; }          // CommandId — document key
    public Guid VehicleId { get; set; }   // The vehicle created by this command
}
```

**Notes**:
- Stored in the same `SaveChangesAsync` call as the `VehicleAdded` event (transactional).
- Queried by `AddVehicleHandler` before processing to detect duplicates.
- No TTL or cleanup — idempotency records are small and permanent.

---

## 6. HTTP API Surface (Contract Summary)

These endpoints are defined by the api-developer and implemented by the backend-builder. This section establishes the contract from the domain model perspective.

### POST /api/v1/garage/vehicles

Add a vehicle to the authenticated user's garage.

```
Request body:  AddVehicleCommand { commandId, name, make?, model?, year? }
Success:       201 Created — { vehicleId: "guid" }
Idempotent:    If commandId was already processed, returns 200 OK — { vehicleId: "guid" }
Errors:
  - 401: Unauthenticated
  - 400: Validation errors (ProblemDetails with errors object)
```

### GET /api/v1/garage/summary

Fetch the dashboard garage summary for the authenticated user.

```
Response:      200 OK — GarageSummary { vehicles: GarageSummaryVehicle[] }
Vehicles sorted by createdAt descending (newest first).
Errors:
  - 401: Unauthenticated
```

### GET /api/v1/garage/vehicles/{vehicleId}

Fetch a single vehicle's details for the vehicle detail page.

```
Response:      200 OK — VehicleReadModel
Access:        Vehicle must belong to authenticated user
Errors:
  - 401: Unauthenticated
  - 404: Vehicle not found OR belongs to another user (no existence leakage)
```

### PUT /api/v1/garage/vehicles/{vehicleId}

Update a vehicle's profile fields.

```
Request body:  UpdateVehicleCommand { vehicleId, name, make?, model?, year? }
               (vehicleId in body must match URL; or derive from URL only)
Success:       200 OK
Errors:
  - 401: Unauthenticated
  - 400: Validation errors (ProblemDetails with errors object)
  - 404: Vehicle not found OR belongs to another user
```

---

## 7. Marten Configuration Changes

### Event Registration

```csharp
// Add to existing Marten configuration in Program.cs
options.Events.AddEventType<VehicleAdded>();
options.Events.AddEventType<VehicleUpdated>();
```

### Projection Registration

```csharp
options.Projections.Add<VehicleProjection>(ProjectionLifecycle.Inline);
options.Projections.Add<GarageSummaryProjection>(ProjectionLifecycle.Inline);
```

### Document Registration & Indexes

```csharp
// VehicleReadModel: index on UserId for filtered queries
options.Schema.For<VehicleReadModel>()
    .Index(x => x.UserId);

// GarageSummary: keyed by UserId (string), no additional indexes needed
// (loaded directly by document ID)

// ProcessedAddVehicle: keyed by CommandId (Guid), no additional indexes
// (loaded directly by document ID)
```

### Namespace Convention

Following the established vertical-slice pattern from Sprint 01:

```
gabMileage.Api/
  Garage/
    Commands/
      AddVehicleCommand.cs
      UpdateVehicleCommand.cs
    DomainEvents/
      VehicleAdded.cs
      VehicleUpdated.cs
    Handlers/
      AddVehicleHandler.cs
      UpdateVehicleHandler.cs
    Projections/
      VehicleProjection.cs
      GarageSummaryProjection.cs
    ReadModels/
      VehicleReadModel.cs
      GarageSummary.cs          ← replaces Sprint 01 placeholder
      GarageSummaryVehicle.cs
      ProcessedAddVehicle.cs
    Vehicle.cs                  ← aggregate root
```

---

## 8. Decisions & Rationale

### D-01: Vehicle Aggregate Uses Guid ID with String Stream Key

- **Who**: domain-modeler
- **What**: Vehicle has a `Guid Id` but its Marten stream key is the string `"vehicle-{id}"`, consistent with Sprint 01's `StreamIdentity.AsString` setting.
- **Why**: Sprint 01 established `StreamIdentity.AsString` globally to accommodate the User aggregate's WorkOS string ID. Vehicle uses a Guid for its identity (standard for domain entities) but formats it as a string for the stream key. This follows the Marten skill's `"{type}-{guid}"` convention and is consistent with the existing User stream pattern `"user-{workosId}"`.
- **Alternatives considered**:
  - Use raw Guid stream identity → rejected: would require changing the global `StreamIdentity` setting, breaking User streams
  - Use string UUID as Vehicle identity → rejected: Guid is more natural for generated identities; string-formatted Guid gives us both
- **Confidence**: High

### D-02: CommandId for Server-Side Idempotency

- **Who**: domain-modeler
- **What**: `AddVehicleCommand` includes a client-generated `commandId` (Guid/UUID). The handler uses a `ProcessedAddVehicle` Marten document to track processed commands. Duplicate `commandId` submissions return the existing `vehicleId` without emitting a new event.
- **Why**: The sprint brief explicitly requires server-side idempotency for AddVehicle using `commandId`. Client-side prevention (disabling the button) is insufficient — network retries, browser refreshes, or race conditions can produce duplicates. A server-side check ensures exactly-once semantics for vehicle creation.
- **Alternatives considered**:
  - Use `commandId` as the `vehicleId` (deterministic) → rejected: couples vehicle identity to an ephemeral client concept; breaks if client reuses a commandId for a different vehicle
  - Store commandId on the event only, query events for duplicates → rejected: expensive scan; Marten documents are optimized for key-based lookup
  - Use `commandId` as stream key → rejected: stream key must follow `"vehicle-{vehicleId}"` convention
  - No server-side idempotency → rejected: sprint brief explicitly requires it
- **Confidence**: High

### D-03: VehicleUpdated Does NOT Carry UserId

- **Who**: domain-modeler
- **What**: The `VehicleUpdated` event contains only `VehicleId`, the changed profile fields, and `OccurredAt`. It does not include `UserId`.
- **Why**: Ownership is established once in `VehicleAdded` and is immutable. Including `UserId` in every update event would be denormalization that adds no domain value. The handler verifies ownership before emitting, so the event itself doesn't need the user context.
- **Alternatives considered**:
  - Include `UserId` in every event → see Decision D-07 for the tradeoff with GarageSummaryProjection routing
- **Confidence**: Medium — depends on D-07 resolution

### D-04: GarageSummary Sorted by CreatedAt Descending (In Projection)

- **Who**: domain-modeler
- **What**: The `GarageSummaryProjection` maintains vehicles in newest-first order by inserting new vehicles at position 0 of the list.
- **Why**: The sprint brief specifies "ordered most-recently-added first." Sorting in the projection (at write time) rather than at query time keeps the read path trivially simple — the API returns the list as-is without sorting. The list is expected to be small (personal garage, typically <20 vehicles).
- **Alternatives considered**:
  - Sort at query time → rejected: adds complexity to every read; projection is the natural place for denormalized read models
  - Sort alphabetically → rejected: sprint brief explicitly says newest-first
  - Include a `sortOrder` field → rejected: over-engineering for a small list with a single sort criterion
- **Confidence**: High

### D-05: PhotoUrl Fields Included as Null Placeholders

- **Who**: domain-modeler
- **What**: `VehicleReadModel` and `GarageSummaryVehicle` include `PhotoUrl` and `PhotoThumbnailUrl` as nullable fields, always `null` until Sprint 06.
- **Why**: The sprint brief's data contract specifies these fields. Including them now establishes the API shape so the frontend can build against it. Adding fields later is an extend-only change, but including them now avoids even that minor churn.
- **Alternatives considered**:
  - Omit until Sprint 06 → rejected: sprint brief explicitly includes them in the contract
- **Confidence**: High

### D-06: 404 (Not 403) for Unauthorized Vehicle Access

- **Who**: domain-modeler
- **What**: When a user attempts to access a vehicle that doesn't exist OR belongs to a different user, the API returns 404 Not Found. Never 403 Forbidden.
- **Why**: Returning 403 would confirm the vehicle exists to an unauthorized user, leaking information. 404 is the standard pattern for this (GitHub does the same for private repos). The sprint brief explicitly calls for this behavior.
- **Alternatives considered**:
  - 403 Forbidden → rejected: leaks existence information
  - 401 Unauthorized → rejected: the user IS authenticated; it's an authorization issue, but 404 is the correct security choice
- **Confidence**: High

### D-07: Include UserId in VehicleUpdated for GarageSummary Projection Routing

- **Who**: domain-modeler
- **What**: Despite Decision D-03's principle that ownership is established in `VehicleAdded`, the `GarageSummaryProjection` needs to route `VehicleUpdated` events to the correct user's summary document. **Resolution**: Include `UserId` as a denormalized field in `VehicleUpdated` to simplify projection routing.
- **Why**: Marten's `MultiStreamProjection` requires an `Identity<TEvent>()` mapping to determine which document an event belongs to. Without `UserId` on `VehicleUpdated`, the projection would need a custom event grouper that performs a database lookup (loading the `VehicleReadModel` to find the `UserId`), which adds complexity and potential race conditions with Inline lifecycle. Including `UserId` in the event is a pragmatic denormalization that keeps the projection simple and efficient.
- **Alternatives considered**:
  - Custom event grouper with DB lookup → rejected: adds latency and complexity to Inline projection; potential ordering issues
  - Separate GarageSummary refresh via a Wolverine handler (not a projection) → rejected: loses the simplicity of Marten's built-in projection infrastructure
  - Don't update GarageSummary on VehicleUpdated (only on VehicleAdded) → rejected: dashboard cards show name/make/model which can change
- **Confidence**: High
- **Impact on D-03**: D-03 is partially revised. `VehicleUpdated` WILL include `UserId` for projection routing purposes. The field is still immutable and set from the handler's ownership check.

**Revised VehicleUpdated record**:

```csharp
public sealed record VehicleUpdated(
    Guid VehicleId,
    string UserId,     // denormalized for GarageSummary projection routing
    string Name,
    string? Make,
    string? Model,
    int? Year,
    Instant OccurredAt
);
```

**Revised Vehicle aggregate Apply**:

```csharp
public void Apply(VehicleUpdated e)
{
    Name = e.Name;
    Make = e.Make;
    Model = e.Model;
    Year = e.Year;
    UpdatedAt = e.OccurredAt;
    // UserId intentionally not updated — it was set in VehicleAdded and is immutable
}
```

### D-08: ProcessedAddVehicle as a Marten Document (Not Event-Sourced)

- **Who**: domain-modeler
- **What**: Idempotency tracking uses a simple Marten document (`ProcessedAddVehicle`) stored via `session.Store()`, not an event-sourced aggregate.
- **Why**: The idempotency record is infrastructure, not a domain concept. It doesn't need an event history or projections — it's a simple key-value lookup (commandId → vehicleId). Using `session.Store()` within the same `SaveChangesAsync()` call as the `StartStream()` gives us transactional consistency without the overhead of event sourcing.
- **Alternatives considered**:
  - Event-sourced idempotency aggregate → rejected: over-engineering for a simple lookup; events for "command was processed" aren't domain events
  - External cache (Redis) → rejected: loses transactional consistency with the event store
  - In-memory cache → rejected: lost on restart, not durable
- **Confidence**: High

### D-09: GarageSummary Replaces Sprint 01 Placeholder

- **Who**: domain-modeler
- **What**: The `GarageSummary` and `GarageSummaryVehicle` classes from Sprint 01 (`gabMileage.Api.Garage.ReadModels`) are replaced with fully-defined types in Sprint 02. The Sprint 01 endpoint that returned a hardcoded empty response is replaced with a projection-backed query.
- **Why**: Sprint 01 established the contract shape with placeholder types. Sprint 02 fills in the real implementation. The `UserId` field on `GarageSummary` becomes the Marten document `Id` (string-keyed, matching `AuthContext.UserId`).
- **Alternatives considered**:
  - Keep placeholder and add a new type → rejected: unnecessary indirection; the placeholder was explicitly designed to be replaced
- **Confidence**: High

### D-10: No Archive/Delete Events in Sprint 02

- **Who**: domain-modeler
- **What**: No `VehicleArchived`, `VehicleRestored`, or `VehicleDeleted` events are modeled in this sprint.
- **Why**: The sprint brief explicitly lists archiving and deleting as out of scope (Sprint 06). The domain model should reflect what's built, not what might be built. Adding these events later is straightforward — just new events on the existing vehicle stream.
- **Alternatives considered**:
  - Define events now, implement later → rejected: speculative; the exact archive/restore flow may evolve
- **Confidence**: High

---

## 9. Sprint Boundary — What's NOT Modeled

The following domain concepts are explicitly **out of scope** for this sprint's event catalog:

| Concept | Sprint | Notes |
|---|---|---|
| Vehicle photo upload/display | Sprint 06 | `PhotoUrl` fields are null placeholders |
| FuelLog aggregate | Sprint 03 | `FuelLogRecorded`, `FuelLogUpdated`, etc. |
| MaintenanceItem / MaintenanceRecord | Sprint 04 | Maintenance domain events |
| Dashboard attention strip / badges | Sprint 05 | Badge counts on vehicle cards |
| Vehicle archive / restore / delete | Sprint 06 | `VehicleArchived`, `VehicleRestored` |
| Searching / filtering vehicles | Future | Not in any current sprint brief |
| Manual card reordering | Future | Not in any current sprint brief |

---

## 10. Event Catalog Summary

| Event | Stream | Trigger | Sprint |
|---|---|---|---|
| `UserProvisioned` | `user-{userId}` | Gateway auth callback (first login) | 01 |
| `VehicleAdded` | `vehicle-{vehicleId}` | AddVehicleCommand (POST /api/v1/garage/vehicles) | **02** |
| `VehicleUpdated` | `vehicle-{vehicleId}` | UpdateVehicleCommand (PUT /api/v1/garage/vehicles/{id}) | **02** |

## 11. Command Catalog Summary

| Command | Handler | Emits | Caller |
|---|---|---|---|
| `ProvisionUser` | `ProvisionUserHandler` | `UserProvisioned` | Gateway auth callback |
| `AddVehicleCommand` | `AddVehicleHandler` | `VehicleAdded` | Frontend → POST /api/v1/garage/vehicles |
| `UpdateVehicleCommand` | `UpdateVehicleHandler` | `VehicleUpdated` | Frontend → PUT /api/v1/garage/vehicles/{id} |

## 12. Projection Summary

| Projection | Type | Lifecycle | Source Events | Read Model | Key Type |
|---|---|---|---|---|---|
| `UserProfileProjection` | SingleStreamProjection | Inline | `UserProvisioned` | `UserProfile` | `string` |
| `VehicleProjection` | SingleStreamProjection | Inline | `VehicleAdded`, `VehicleUpdated` | `VehicleReadModel` | `Guid` |
| `GarageSummaryProjection` | MultiStreamProjection | Inline | `VehicleAdded`, `VehicleUpdated` | `GarageSummary` | `string` (UserId) |
