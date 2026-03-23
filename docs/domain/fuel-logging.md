# Domain Model — Sprint 03: Fuel Logging

> **Sprint**: fuel-logging
> **Modeler**: domain-modeler
> **Date**: 2026-03-23
> **Status**: 🔒 Locked — event catalog finalized before builders begin
> **Depends on**: Sprint 02 (garage-vehicles) — `Vehicle` aggregate, `VehicleReadModel`, string-keyed Marten streams

---

## Overview

Sprint 03 introduces the **FuelLog** domain — users can record, view, edit, and delete fill-ups for their vehicles. Each fill-up captures an odometer reading and fuel data (any two of three fuel fields; the third is auto-calculated). The system computes per-fill-up MPG server-side and suppresses it when the data is unreliable (first fill-up, partial fill-ups, or fill-ups adjacent to partial fill-ups).

The sprint introduces three domain events (`FuelLogRecorded`, `FuelLogUpdated`, `FuelLogDeleted`), three commands, one multi-stream projection (`FuelLogListProjection`), and a set of value objects that carry domain invariants for fuel field precision and odometer constraints.

**Key modeling decisions in this sprint:**

- FuelLog events live on per-vehicle streams (`vehicle-{vehicleId}`), not per-fuel-log streams — see Decision D-01.
- MPG is computed in the projection (read model), not stored in events — see Decision D-02.
- The `calculatedField` marker is stored in events to preserve which field the user entered vs. which was auto-calculated — see Decision D-03.
- Idempotency for `RecordFuelLogCommand` uses the same `ProcessedCommand` pattern as `AddVehicleCommand` — see Decision D-04.

---

## 1. Aggregates & Entities

### FuelLog (Not a Standalone Aggregate — Events on Vehicle Stream)

FuelLog events are appended to the **vehicle stream** (`vehicle-{vehicleId}`), not to their own independent streams. The `FuelLog` is a domain entity within the vehicle's event history, not a top-level aggregate root. See Decision D-01 for rationale.

The Vehicle aggregate is not directly modified — fuel log events are additional event types on the same stream, consumed by a dedicated projection (`FuelLogListProjection`) to build the fuel log read model.

**FuelLog entity fields** (as stored in events):

| Property | Type | Description |
|---|---|---|
| **FuelLogId** | `Guid` | Unique identity for this fill-up. Generated server-side. |
| **VehicleId** | `Guid` | The vehicle this fill-up belongs to. From URL/command. |
| **UserId** | `string` | WorkOS user ID. Denormalized for ownership verification & projection routing. |
| **Date** | `LocalDate` | Fill-up date (NodaTime). User-chosen, not future. |
| **Odometer** | `int` | Odometer reading in miles. Positive integer, max 9,999,999. |
| **Gallons** | `decimal?` | Gallons pumped. Up to 3 decimal places, max 999.999. Nullable. |
| **TotalCost** | `decimal?` | Total cost in dollars. Up to 2 decimal places, max 9,999.99. Nullable. |
| **PricePerGallon** | `decimal?` | Price per gallon in dollars. Up to 3 decimal places, max 99.999. Nullable. |
| **CalculatedField** | `string?` | Which field was auto-calculated: `"gallons"`, `"totalCost"`, `"pricePerGallon"`, or `null` if all were user-entered. |
| **IsPartialFillUp** | `bool` | Whether this was a partial fill (affects MPG calculation). |
| **Notes** | `string?` | Free-text notes. Max 500 characters. Nullable. |
| **OccurredAt** | `Instant` | Server timestamp when the event was created. |

**Stream ID**: `vehicle-{vehicleId}` (shared with Vehicle aggregate events)

**Invariants**:

1. `FuelLogId` must be a non-empty Guid (generated server-side).
2. `VehicleId` must reference an existing vehicle owned by the authenticated user.
3. `Date` must be a valid `LocalDate`, not in the future (compared against server date in user's context).
4. `Odometer` must be a positive integer ≤ 9,999,999.
5. At least two of {`Gallons`, `TotalCost`, `PricePerGallon`} must be provided on submit. The third is auto-calculated by the frontend and sent to the server.
6. `Gallons`, if provided: positive `decimal`, ≤ 3 decimal places, max 999.999. $0$ is not valid (must be positive). But note: `TotalCost` of $0.00 IS valid (free fuel).
7. `TotalCost`, if provided: non-negative `decimal`, ≤ 2 decimal places, max 9,999.99. $0.00 is valid.
8. `PricePerGallon`, if provided: non-negative `decimal`, ≤ 3 decimal places, max 99.999. $0.000 is valid.
9. `Notes`, if provided: max 500 characters.
10. `CalculatedField` must be one of `"gallons"`, `"totalCost"`, `"pricePerGallon"`, or `null`.
11. If `CalculatedField` is non-null, the corresponding fuel field must also be non-null (the calculation result).
12. All three fuel fields must be non-null on a persisted event — the frontend always sends all three (two user-entered + one calculated, or three user-entered).

**Note on Invariant 12**: The command accepts nullable fuel fields because the "conditional: at least two" rule applies. However, the handler computes the third field server-side if not provided (defensive fallback), ensuring all three are present in the emitted event. The frontend is expected to send all three after auto-calculation, but the server does not blindly trust this.

---

## 2. Value Objects

This sprint introduces value objects for fuel domain types that carry precision constraints and validation rules beyond simple range checks.

### Odometer

Represents a vehicle odometer reading in miles.

```csharp
public readonly record struct Odometer
{
    public int Value { get; }

    public Odometer(int value)
    {
        if (value is <= 0 or > 9_999_999)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Odometer must be a positive integer ≤ 9,999,999.");
        Value = value;
    }

    public static implicit operator int(Odometer o) => o.Value;
}
```

**Constraints**: Positive integer, max 9,999,999 (7 digits).
**Usage**: Used in command validation. Events store `int` directly (events are flat records; value objects are for validation logic, not serialization). See Decision D-05.

### FuelAmount

Represents a volume of fuel in gallons.

```csharp
public readonly record struct FuelAmount
{
    public decimal Value { get; }

    public FuelAmount(decimal value)
    {
        if (value <= 0m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Fuel amount must be positive.");
        if (value > 999.999m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Fuel amount must be ≤ 999.999.");
        if (decimal.Round(value, 3) != value)
            throw new ArgumentException(
                "Fuel amount allows at most 3 decimal places.", nameof(value));
        Value = value;
    }
}
```

**Constraints**: Positive, ≤ 3 decimal places, max 999.999.

### FuelCost

Represents a monetary cost for fuel (total cost of a fill-up).

```csharp
public readonly record struct FuelCost
{
    public decimal Value { get; }

    public FuelCost(decimal value)
    {
        if (value < 0m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Fuel cost must be non-negative.");
        if (value > 9_999.99m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Fuel cost must be ≤ 9,999.99.");
        if (decimal.Round(value, 2) != value)
            throw new ArgumentException(
                "Fuel cost allows at most 2 decimal places.", nameof(value));
        Value = value;
    }
}
```

**Constraints**: Non-negative (≥ $0.00 — free fuel allowed), ≤ 2 decimal places, max 9,999.99.

### PricePerGallon

Represents a fuel price per gallon.

```csharp
public readonly record struct PricePerGallon
{
    public decimal Value { get; }

    public PricePerGallon(decimal value)
    {
        if (value < 0m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Price per gallon must be non-negative.");
        if (value > 99.999m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Price per gallon must be ≤ 99.999.");
        if (decimal.Round(value, 3) != value)
            throw new ArgumentException(
                "Price per gallon allows at most 3 decimal places.", nameof(value));
        Value = value;
    }
}
```

**Constraints**: Non-negative (≥ $0.000), ≤ 3 decimal places (gas price 9/10ths convention), max 99.999.

### CalculatedFieldKind

Enum-like discriminator for which fuel field was auto-calculated.

```csharp
public static class CalculatedFieldKind
{
    public const string Gallons = "gallons";
    public const string TotalCost = "totalCost";
    public const string PricePerGallon = "pricePerGallon";

    public static bool IsValid(string? value) =>
        value is null or Gallons or TotalCost or PricePerGallon;
}
```

**Rationale**: Using `string` constants rather than a C# `enum` because this value is serialized to JSON and stored in events. String constants give stable wire representation without enum-int mapping fragility.

---

## 3. Event Catalog

### FuelLogRecorded

Records that a user has logged a new fill-up for a vehicle.

```
Event:       FuelLogRecorded
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle aggregate events)

Payload:
  - FuelLogId       : Guid      — New fill-up identity. Generated server-side.
  - VehicleId       : Guid      — The vehicle this fill-up belongs to.
  - UserId          : string    — WorkOS user ID. Denormalized for ownership/routing.
  - CommandId       : Guid      — Client-generated idempotency key.
  - Date            : LocalDate — Fill-up date (NodaTime). Not future.
  - Odometer        : int       — Odometer reading in miles. Positive, ≤ 9,999,999.
  - Gallons         : decimal?  — Gallons pumped. Positive, ≤3 dp, max 999.999. Null if not applicable (see note).
  - TotalCost       : decimal?  — Total cost. Non-negative, ≤2 dp, max 9,999.99.
  - PricePerGallon  : decimal?  — Price/gal. Non-negative, ≤3 dp, max 99.999.
  - CalculatedField : string?   — "gallons" | "totalCost" | "pricePerGallon" | null
  - IsPartialFillUp : bool      — Whether this was a partial fill.
  - Notes           : string?   — Free-text notes. Max 500 chars.
  - OccurredAt      : Instant   — NodaTime server timestamp.

Invariants:
  - FuelLogId must be a non-empty Guid
  - VehicleId must reference an existing vehicle owned by the authenticated user
  - CommandId must be a non-empty Guid (unique per user for this vehicle)
  - Date must not be in the future
  - Odometer must be positive integer ≤ 9,999,999
  - At least two of {Gallons, TotalCost, PricePerGallon} must be non-null
  - All three fuel fields are non-null in the persisted event (third calculated server-side if needed)
  - Notes (if present) ≤ 500 chars
  - CalculatedField must be valid per CalculatedFieldKind

Trigger:
  - Frontend POST to record-fuel-log endpoint with RecordFuelLogCommand payload
```

**C# record definition**:

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

**Note on non-nullable fuel fields in the event**: Even though the command accepts nullable fuel fields (at least two required), the handler always ensures all three are present in the emitted event. If the frontend sends all three, they're used as-is. If only two are sent, the handler calculates the third. This simplifies downstream projections — they never need to handle missing fuel data.

### FuelLogUpdated

Records that a user has modified an existing fill-up.

```
Event:       FuelLogUpdated
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle aggregate events)

Payload:
  - FuelLogId       : Guid      — The fill-up being updated.
  - VehicleId       : Guid      — The vehicle this fill-up belongs to.
  - UserId          : string    — WorkOS user ID. Denormalized for ownership verification.
  - Date            : LocalDate — Updated fill-up date.
  - Odometer        : int       — Updated odometer reading.
  - Gallons         : decimal   — Updated gallons (all three always present — see FuelLogRecorded note).
  - TotalCost       : decimal   — Updated total cost.
  - PricePerGallon  : decimal   — Updated price/gal.
  - CalculatedField : string?   — Which field was auto-calculated, or null.
  - IsPartialFillUp : bool      — Updated partial flag.
  - Notes           : string?   — Updated notes.
  - OccurredAt      : Instant   — NodaTime server timestamp.

Invariants:
  - FuelLogId must reference an existing fuel log on this vehicle stream
  - VehicleId must match the stream's vehicle
  - UserId must match the vehicle's owner (ownership check)
  - Same validation rules as FuelLogRecorded for all other fields

Trigger:
  - Frontend PUT to update-fuel-log endpoint with UpdateFuelLogCommand payload
```

**C# record definition**:

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

### FuelLogDeleted

Records that a user has removed a fill-up.

```
Event:       FuelLogDeleted
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle aggregate events)

Payload:
  - FuelLogId   : Guid    — The fill-up being deleted.
  - VehicleId   : Guid    — The vehicle this fill-up belongs to.
  - UserId      : string  — WorkOS user ID. Denormalized for ownership verification.
  - OccurredAt  : Instant — NodaTime server timestamp.

Invariants:
  - FuelLogId must reference an existing (non-deleted) fuel log on this vehicle stream
  - VehicleId must match the stream's vehicle
  - UserId must match the vehicle's owner

Trigger:
  - Frontend DELETE to delete-fuel-log endpoint with DeleteFuelLogCommand payload
```

**C# record definition**:

```csharp
public sealed record FuelLogDeleted(
    Guid FuelLogId,
    Guid VehicleId,
    string UserId,
    Instant OccurredAt
);
```

---

## 4. Command Catalog

### RecordFuelLogCommand

Records a new fill-up for a vehicle. Idempotent via `commandId`.

```
Command:     RecordFuelLogCommand
Handler:     RecordFuelLogHandler (static class, Wolverine command handler)
Emits:       FuelLogRecorded (only if commandId has not been processed)
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - CommandId      : Guid      — Client-generated UUID for idempotency
  - VehicleId      : Guid      — Target vehicle (from URL route)
  - Date           : LocalDate — Fill-up date
  - Odometer       : int       — Odometer reading
  - Gallons        : decimal?  — Gallons pumped (nullable — at least 2 of 3 required)
  - TotalCost      : decimal?  — Total cost (nullable)
  - PricePerGallon : decimal?  — Price per gallon (nullable)
  - CalculatedField: string?   — Which field was auto-calculated by the frontend
  - IsPartialFillUp: bool      — Partial fill flag
  - Notes          : string?   — Free-text notes

Validation:
  - CommandId required, non-empty Guid
  - VehicleId required, must reference an existing vehicle owned by the authenticated user
  - Date required, valid LocalDate, not in the future
  - Odometer required, positive integer ≤ 9,999,999
  - At least two of {Gallons, TotalCost, PricePerGallon} must be non-null
  - Gallons (if present): positive, ≤ 3 dp, max 999.999
  - TotalCost (if present): non-negative, ≤ 2 dp, max 9,999.99
  - PricePerGallon (if present): non-negative, ≤ 3 dp, max 99.999
  - Notes (if present): max 500 chars
  - CalculatedField (if present): must be one of "gallons", "totalCost", "pricePerGallon"
  - If odometer is lower than the most recent prior reading for this vehicle:
    NOT an error — handler allows it (odometer rollover, correction, backdated entry).
    The API may return a warning in the response for the frontend to display.

Idempotency:
  - Handler queries for an existing ProcessedRecordFuelLog document keyed by (UserId, CommandId)
  - If found, returns the previously created FuelLogId (no event emitted, no error)
  - If not found, generates a new FuelLogId, emits FuelLogRecorded, stores idempotency marker

Caller:
  - Frontend via POST /api/v1/vehicles/{vehicleId}/fuel-logs
```

**C# record definition**:

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

**Handler sketch**:

```csharp
public static class RecordFuelLogHandler
{
    public static async Task<RecordFuelLogResult> Handle(
        RecordFuelLogCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Validate CommandId
        if (command.CommandId == Guid.Empty)
            throw new FuelLogValidationException(/* commandId required */);

        // 2. Idempotency check
        var markerId = ProcessedCommandId.From(auth.UserId, command.CommandId);
        var processed = await session.LoadAsync<ProcessedRecordFuelLog>(markerId, ct);
        if (processed is not null)
            return new RecordFuelLogResult(processed.FuelLogId, IsDuplicate: true);

        // 3. Ownership check: load vehicle read model
        var vehicleStreamId = $"vehicle-{command.VehicleId}";
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 4. Validate & normalize fuel fields
        var (gallons, totalCost, pricePerGallon, calculatedField) =
            FuelFieldCalculator.NormalizeAndValidate(
                command.Gallons, command.TotalCost, command.PricePerGallon,
                command.CalculatedField);

        // 5. Validate other fields (date, odometer, notes)
        FuelLogInput.Validate(command.Date, command.Odometer, command.Notes, clock);

        // 6. Emit event
        var fuelLogId = Guid.NewGuid();
        var now = clock.GetCurrentInstant();

        var recorded = new FuelLogRecorded(
            fuelLogId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            command.Date,
            command.Odometer,
            gallons,
            totalCost,
            pricePerGallon,
            calculatedField,
            command.IsPartialFillUp,
            command.Notes?.Trim(),
            now);

        session.Events.Append(vehicleStreamId, recorded);

        // 7. Store idempotency marker
        session.Insert(new ProcessedRecordFuelLog(markerId, auth.UserId, fuelLogId, now));

        await session.SaveChangesAsync(ct);
        return new RecordFuelLogResult(fuelLogId, IsDuplicate: false);
    }
}
```

### UpdateFuelLogCommand

Updates an existing fill-up. No idempotency key — updates are idempotent by nature (PUT semantics).

```
Command:     UpdateFuelLogCommand
Handler:     UpdateFuelLogHandler (static class, Wolverine command handler)
Emits:       FuelLogUpdated
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - FuelLogId      : Guid      — The fill-up to update (from URL route)
  - VehicleId      : Guid      — The vehicle (from URL route)
  - Date           : LocalDate — Updated fill-up date
  - Odometer       : int       — Updated odometer reading
  - Gallons        : decimal?  — Updated gallons (nullable — at least 2 of 3 required)
  - TotalCost      : decimal?  — Updated total cost (nullable)
  - PricePerGallon : decimal?  — Updated price per gallon (nullable)
  - CalculatedField: string?   — Which field was auto-calculated
  - IsPartialFillUp: bool      — Updated partial flag
  - Notes          : string?   — Updated notes

Validation:
  - Same rules as RecordFuelLogCommand (except no CommandId)
  - FuelLogId required, must reference an existing non-deleted fuel log on this vehicle
  - VehicleId required, vehicle must exist and belong to authenticated user

Access control:
  - Handler loads the FuelLogList read model and verifies:
    1. Vehicle exists and belongs to authenticated user
    2. FuelLogId exists in the vehicle's fuel log list (not deleted)
  - If not found → 404

Caller:
  - Frontend via PUT /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}
```

**C# record definition**:

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

**Handler sketch**:

```csharp
public static class UpdateFuelLogHandler
{
    public static async Task Handle(
        UpdateFuelLogCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Ownership check
        var vehicleStreamId = $"vehicle-{command.VehicleId}";
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 2. Verify fuel log exists
        var fuelLogList = await session.LoadAsync<FuelLogList>(
            FuelLogListId.From(command.VehicleId), ct);
        if (fuelLogList is null || !fuelLogList.Logs.Any(l => l.FuelLogId == command.FuelLogId))
            throw new FuelLogNotFoundException(command.FuelLogId);

        // 3. Validate & normalize
        var (gallons, totalCost, pricePerGallon, calculatedField) =
            FuelFieldCalculator.NormalizeAndValidate(
                command.Gallons, command.TotalCost, command.PricePerGallon,
                command.CalculatedField);
        FuelLogInput.Validate(command.Date, command.Odometer, command.Notes, clock);

        // 4. Emit event
        var now = clock.GetCurrentInstant();
        session.Events.Append(vehicleStreamId, new FuelLogUpdated(
            command.FuelLogId,
            command.VehicleId,
            auth.UserId,
            command.Date,
            command.Odometer,
            gallons,
            totalCost,
            pricePerGallon,
            calculatedField,
            command.IsPartialFillUp,
            command.Notes?.Trim(),
            now));

        await session.SaveChangesAsync(ct);
    }
}
```

### DeleteFuelLogCommand

Deletes a fill-up. Uses confirmation dialog on frontend (not undo); server-side is a simple event append.

```
Command:     DeleteFuelLogCommand
Handler:     DeleteFuelLogHandler (static class, Wolverine command handler)
Emits:       FuelLogDeleted
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - FuelLogId : Guid — The fill-up to delete (from URL route)
  - VehicleId : Guid — The vehicle (from URL route)

Validation:
  - FuelLogId required, must reference an existing non-deleted fuel log
  - VehicleId required, vehicle must exist and belong to authenticated user

Access control:
  - Same as UpdateFuelLogCommand

Caller:
  - Frontend via DELETE /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}
```

**C# record definition**:

```csharp
public sealed record DeleteFuelLogCommand(
    Guid FuelLogId,
    Guid VehicleId
);
```

**Handler sketch**:

```csharp
public static class DeleteFuelLogHandler
{
    public static async Task Handle(
        DeleteFuelLogCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Ownership check
        var vehicleStreamId = $"vehicle-{command.VehicleId}";
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 2. Verify fuel log exists
        var fuelLogList = await session.LoadAsync<FuelLogList>(
            FuelLogListId.From(command.VehicleId), ct);
        if (fuelLogList is null || !fuelLogList.Logs.Any(l => l.FuelLogId == command.FuelLogId))
            throw new FuelLogNotFoundException(command.FuelLogId);

        // 3. Emit deletion event
        var now = clock.GetCurrentInstant();
        session.Events.Append(vehicleStreamId, new FuelLogDeleted(
            command.FuelLogId,
            command.VehicleId,
            auth.UserId,
            now));

        await session.SaveChangesAsync(ct);
    }
}
```

---

## 5. Projections

### FuelLogListProjection (Multi-Stream)

Builds the `FuelLogList` read model from fuel log events across vehicle streams. Keyed by vehicle ID. Uses `MultiStreamProjection` with `Inline` lifecycle for immediate read-after-write consistency.

```
Projection:    FuelLogListProjection
Type:          MultiStreamProjection<FuelLogList, string>
Lifecycle:     Inline
Source Events: FuelLogRecorded, FuelLogUpdated, FuelLogDeleted

Read Model:    FuelLogList (keyed by "fuellog-{vehicleId}")
```

**Why MultiStreamProjection?** The events live on the vehicle stream (`vehicle-{vehicleId}`) but the read model is keyed differently (`fuellog-{vehicleId}`) to separate fuel log data from the Vehicle read model. MultiStreamProjection allows routing events from the vehicle stream to a different document key.

**Read model shapes**:

```csharp
public sealed class FuelLogList
{
    public string Id { get; set; } = string.Empty;            // "fuellog-{vehicleId}"
    public Guid VehicleId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IList<FuelLogEntry> Logs { get; set; } = [];
    public int? LastOdometer { get; set; }                     // Most recent odometer reading
}

public sealed class FuelLogEntry
{
    public Guid FuelLogId { get; set; }
    public LocalDate Date { get; set; }
    public int Odometer { get; set; }
    public decimal Gallons { get; set; }
    public decimal TotalCost { get; set; }
    public decimal PricePerGallon { get; set; }
    public string? CalculatedField { get; set; }
    public bool IsPartialFillUp { get; set; }
    public string? Notes { get; set; }
    public decimal? Mpg { get; set; }                          // Computed in projection; null if suppressed
    public Instant CreatedAt { get; set; }
}
```

**Projection implementation sketch**:

```csharp
public sealed class FuelLogListProjection : MultiStreamProjection<FuelLogList, string>
{
    public FuelLogListProjection()
    {
        Lifecycle = ProjectionLifecycle.Inline;

        // Route all fuel log events to the fuellog-{vehicleId} document
        Identity<FuelLogRecorded>(e => FuelLogListId.From(e.VehicleId));
        Identity<FuelLogUpdated>(e => FuelLogListId.From(e.VehicleId));
        Identity<FuelLogDeleted>(e => FuelLogListId.From(e.VehicleId));

        ProjectEvent<FuelLogRecorded>((list, e) =>
        {
            list.VehicleId = e.VehicleId;
            list.UserId = e.UserId;

            list.Logs.Add(new FuelLogEntry
            {
                FuelLogId = e.FuelLogId,
                Date = e.Date,
                Odometer = e.Odometer,
                Gallons = e.Gallons,
                TotalCost = e.TotalCost,
                PricePerGallon = e.PricePerGallon,
                CalculatedField = e.CalculatedField,
                IsPartialFillUp = e.IsPartialFillUp,
                Notes = e.Notes,
                CreatedAt = e.OccurredAt,
            });

            RecalculateMpgAndSort(list);
        });

        ProjectEvent<FuelLogUpdated>((list, e) =>
        {
            var entry = list.Logs.FirstOrDefault(l => l.FuelLogId == e.FuelLogId);
            if (entry is not null)
            {
                entry.Date = e.Date;
                entry.Odometer = e.Odometer;
                entry.Gallons = e.Gallons;
                entry.TotalCost = e.TotalCost;
                entry.PricePerGallon = e.PricePerGallon;
                entry.CalculatedField = e.CalculatedField;
                entry.IsPartialFillUp = e.IsPartialFillUp;
                entry.Notes = e.Notes;
            }

            RecalculateMpgAndSort(list);
        });

        ProjectEvent<FuelLogDeleted>((list, e) =>
        {
            list.Logs.RemoveAll(l => l.FuelLogId == e.FuelLogId);
            RecalculateMpgAndSort(list);
        });
    }

    private static void RecalculateMpgAndSort(FuelLogList list)
    {
        // Sort: date ascending first (for MPG calculation), then reverse for display
        var sorted = list.Logs
            .OrderBy(l => l.Date)
            .ThenBy(l => l.Odometer)
            .ToList();

        // Calculate MPG for each entry
        for (var i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];

            if (i == 0)
            {
                // First fill-up: no prior → suppress MPG
                current.Mpg = null;
                continue;
            }

            var prior = sorted[i - 1];

            if (current.IsPartialFillUp || prior.IsPartialFillUp)
            {
                // Partial fill-up or prior was partial → suppress MPG
                current.Mpg = null;
                continue;
            }

            var distance = current.Odometer - prior.Odometer;
            if (distance <= 0 || current.Gallons <= 0)
            {
                current.Mpg = null;
                continue;
            }

            current.Mpg = Math.Round((decimal)distance / current.Gallons, 1);
        }

        // Store sorted by date desc, odometer desc (display order)
        list.Logs = sorted
            .OrderByDescending(l => l.Date)
            .ThenByDescending(l => l.Odometer)
            .ToList();

        // LastOdometer: the most recent odometer reading (by date, then highest odometer)
        list.LastOdometer = list.Logs.Count > 0 ? list.Logs[0].Odometer : null;
    }
}
```

**Key behaviors**:

1. **Sorting**: Logs are sorted by date descending, then odometer descending for same-date ties. This matches the sprint brief's display order.
2. **MPG calculation**: Computed server-side within the projection on every mutation (add/edit/delete). This ensures MPG is always consistent with the current data. See Decision D-02 for rationale.
3. **MPG suppression**: MPG is `null` (omitted from display) when:
   - First fill-up (no prior odometer to compute distance)
   - Current fill-up is partial (`isPartialFillUp = true`)
   - Prior fill-up (in chronological order) is partial
   - Distance ≤ 0 (odometer didn't advance, or went backwards due to correction)
4. **Recalculation on edit/delete**: When any fuel log is added, edited, or deleted, MPG is recalculated for ALL entries. This handles cascading effects (e.g., deleting a log changes the "prior" log for the one after it).
5. **LastOdometer**: The most recent odometer reading (first entry in display order). Used by the frontend for the "Last reading: X mi" helper text.

### FuelLogListId Helper

```csharp
public static class FuelLogListId
{
    public static string From(Guid vehicleId) => $"fuellog-{vehicleId}";
}
```

### ProcessedRecordFuelLog (Marten Document — Not a Projection)

Idempotency marker for `RecordFuelLogCommand`. Same pattern as Sprint 02's `ProcessedAddVehicle`.

```csharp
public sealed class ProcessedRecordFuelLog
{
    public string Id { get; set; } = string.Empty;    // "{userId}::{commandId}"
    public string UserId { get; set; } = string.Empty;
    public Guid FuelLogId { get; set; }
    public Instant ProcessedAt { get; set; }

    public ProcessedRecordFuelLog() { }

    public ProcessedRecordFuelLog(string id, string userId, Guid fuelLogId, Instant processedAt)
    {
        Id = id;
        UserId = userId;
        FuelLogId = fuelLogId;
        ProcessedAt = processedAt;
    }
}
```

---

## 6. Shared Validation Logic

### FuelFieldCalculator

Encapsulates the server-side logic for normalizing and validating the three fuel fields. Mirrors the frontend auto-calculation rules.

```csharp
public static class FuelFieldCalculator
{
    public static (decimal Gallons, decimal TotalCost, decimal PricePerGallon, string? CalculatedField)
        NormalizeAndValidate(
            decimal? gallons,
            decimal? totalCost,
            decimal? pricePerGallon,
            string? calculatedField)
    {
        // Count how many are provided
        var provided = (gallons.HasValue ? 1 : 0)
                     + (totalCost.HasValue ? 1 : 0)
                     + (pricePerGallon.HasValue ? 1 : 0);

        if (provided < 2)
            throw new FuelLogValidationException(/* "at least two fuel fields required" */);

        // Validate individual field constraints
        if (gallons.HasValue) _ = new FuelAmount(gallons.Value);
        if (totalCost.HasValue) _ = new FuelCost(totalCost.Value);
        if (pricePerGallon.HasValue) _ = new PricePerGallon(pricePerGallon.Value);

        // Calculate missing field if exactly 2 provided
        if (provided == 2)
        {
            if (!gallons.HasValue && totalCost.HasValue && pricePerGallon.HasValue)
            {
                gallons = pricePerGallon.Value > 0
                    ? Math.Round(totalCost.Value / pricePerGallon.Value, 3)
                    : 0m;
                calculatedField = CalculatedFieldKind.Gallons;
            }
            else if (!totalCost.HasValue && gallons.HasValue && pricePerGallon.HasValue)
            {
                totalCost = Math.Round(gallons.Value * pricePerGallon.Value, 2);
                calculatedField = CalculatedFieldKind.TotalCost;
            }
            else if (!pricePerGallon.HasValue && gallons.HasValue && totalCost.HasValue)
            {
                pricePerGallon = gallons.Value > 0
                    ? Math.Round(totalCost.Value / gallons.Value, 3)
                    : 0m;
                calculatedField = CalculatedFieldKind.PricePerGallon;
            }
        }

        // All three provided: if calculatedField is set, trust it.
        // If inconsistent, recalculate pricePerGallon from gallons + totalCost
        // (per sprint brief: gallons + totalCost are most reliable)
        if (provided == 3 && calculatedField is null)
        {
            var expected = gallons!.Value > 0
                ? Math.Round(totalCost!.Value / gallons!.Value, 3)
                : 0m;
            if (Math.Abs(expected - pricePerGallon!.Value) > 0.01m)
            {
                pricePerGallon = expected;
                calculatedField = CalculatedFieldKind.PricePerGallon;
            }
        }

        return (gallons!.Value, totalCost!.Value, pricePerGallon!.Value, calculatedField);
    }
}
```

### FuelLogInput

Encapsulates validation for non-fuel fields.

```csharp
public static class FuelLogInput
{
    public static void Validate(LocalDate date, int odometer, string? notes, IClock clock)
    {
        var errors = new Dictionary<string, string[]>();

        // Date: not in the future
        var today = clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/New_York"])
            .Date;
        if (date > today)
            errors["date"] = ["Date can't be in the future"];

        // Odometer: positive, ≤ 9,999,999
        if (odometer is <= 0 or > 9_999_999)
            errors["odometer"] = ["Enter a valid odometer reading"];

        // Notes: max 500 chars
        if (notes is { Length: > 500 })
            errors["notes"] = ["Notes are too long"];

        if (errors.Count > 0)
            throw new FuelLogValidationException(errors);
    }
}
```

---

## 7. HTTP API Surface (Contract Summary)

These endpoints are defined by the api-developer and implemented by the backend-builder.

### GET /api/v1/vehicles/{vehicleId}/fuel-logs

Fetch the fuel log list for a vehicle, including MPG calculations and lastOdometer.

```
Response:      200 OK — FuelLogListResponse
Access:        Vehicle must belong to authenticated user
Errors:
  - 401: Unauthenticated
  - 404: Vehicle not found OR belongs to another user

Response shape:
{
  vehicleId: string,
  logs: [
    {
      fuelLogId: string,
      date: "2026-03-15",           // LocalDate ISO format
      odometer: 45230,
      gallons: 12.500,
      totalCost: 42.50,
      pricePerGallon: 3.400,
      calculatedField: "pricePerGallon" | null,
      isPartialFillUp: false,
      notes: "Regular unleaded" | null,
      mpg: 28.4 | null,
      createdAt: "2026-03-15T14:30:00Z"  // Instant
    }
  ],
  lastOdometer: 45230 | null
}
```

### POST /api/v1/vehicles/{vehicleId}/fuel-logs

Record a new fill-up for a vehicle.

```
Request body:  RecordFuelLogCommand { commandId, date, odometer, gallons?, totalCost?,
               pricePerGallon?, calculatedField?, isPartialFillUp, notes? }
               Note: vehicleId comes from URL route, not body
Success:       201 Created — { fuelLogId: "guid" }
Idempotent:    If commandId was already processed, returns 200 OK — { fuelLogId: "guid" }
Errors:
  - 401: Unauthenticated
  - 400: Validation errors (ProblemDetails with errors object)
  - 404: Vehicle not found OR belongs to another user
```

### PUT /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}

Update an existing fill-up.

```
Request body:  UpdateFuelLogCommand { date, odometer, gallons?, totalCost?,
               pricePerGallon?, calculatedField?, isPartialFillUp, notes? }
               Note: vehicleId and fuelLogId come from URL route, not body
Success:       200 OK
Errors:
  - 401: Unauthenticated
  - 400: Validation errors (ProblemDetails with errors object)
  - 404: Vehicle or fuel log not found / doesn't belong to user
```

### DELETE /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}

Delete a fill-up.

```
Success:       204 No Content
Errors:
  - 401: Unauthenticated
  - 404: Vehicle or fuel log not found / doesn't belong to user
```

---

## 8. Marten Configuration Changes

### Event Registration

```csharp
// Add to existing Marten configuration
options.Events.AddEventType<FuelLogRecorded>();
options.Events.AddEventType<FuelLogUpdated>();
options.Events.AddEventType<FuelLogDeleted>();
```

### Projection Registration

```csharp
options.Projections.Add<FuelLogListProjection>(ProjectionLifecycle.Inline);
```

### Document Registration

```csharp
// ProcessedRecordFuelLog: idempotency marker
// Keyed by string "{userId}::{commandId}", no additional indexes needed
```

### Namespace Convention

Following the established vertical-slice pattern:

```
gabMileage.Api/
  Garage/
    FuelLogs/
      Commands/
        RecordFuelLogCommand.cs
        UpdateFuelLogCommand.cs
        DeleteFuelLogCommand.cs
      DomainEvents/
        FuelLogRecorded.cs
        FuelLogUpdated.cs
        FuelLogDeleted.cs
      Handlers/
        RecordFuelLogHandler.cs
        UpdateFuelLogHandler.cs
        DeleteFuelLogHandler.cs
      Projections/
        FuelLogListProjection.cs
      ReadModels/
        FuelLogList.cs
        FuelLogEntry.cs
        ProcessedRecordFuelLog.cs
      Validation/
        FuelFieldCalculator.cs
        FuelLogInput.cs
      ValueObjects/
        Odometer.cs
        FuelAmount.cs
        FuelCost.cs
        PricePerGallon.cs
        CalculatedFieldKind.cs
```

---

## 9. Decisions & Rationale

### D-01: FuelLog Events on the Vehicle Stream (Not Separate Streams)

- **Who**: domain-modeler
- **What**: `FuelLogRecorded`, `FuelLogUpdated`, and `FuelLogDeleted` events are appended to the existing `vehicle-{vehicleId}` stream, not to separate `fuellog-{fuelLogId}` streams.
- **Why**: Fuel logs are subordinate entities of a vehicle — they have no meaning outside a vehicle context. Putting them on the vehicle stream keeps all vehicle-related history in one place. The `FuelLogListProjection` (keyed by `fuellog-{vehicleId}`) aggregates all fuel events for a vehicle into a single read model document. This avoids the need for cross-stream queries and simplifies the projection. The projection uses `MultiStreamProjection` to route events from `vehicle-*` streams to `fuellog-*` documents.
- **Alternatives considered**:
  - Separate `fuellog-{fuelLogId}` streams per fill-up → rejected: each fill-up would be its own stream with at most 2–3 events (created, maybe updated, maybe deleted). This fragments the event store with many tiny streams and makes the "list all fuel logs for a vehicle" query expensive (need to scan many streams). It also complicates MPG recalculation — the projection would need to listen to events across many streams.
  - A single `fuellogs-{vehicleId}` stream (all fuel logs for one vehicle) separate from the vehicle stream → rejected: adds a second stream per vehicle with no clear benefit. Keeping fuel log events on the vehicle stream is simpler and keeps the audit trail unified.
- **Confidence**: High

### D-02: MPG Computed in Projection, Not Stored in Events

- **Who**: domain-modeler
- **What**: MPG is a computed property derived from odometer readings and gallons. It is calculated in the `FuelLogListProjection` and stored on the `FuelLogEntry` read model. It is NOT included in the domain events.
- **Why**: MPG depends on the relationship between consecutive fill-ups (current odometer − prior odometer). When a fill-up is added, edited, or deleted, the MPG of neighboring entries can change. Storing MPG in events would mean the event's data is incorrect after a neighboring edit — violating event immutability. Computing it in the projection means it's always derived from current state. The full recalculation on every mutation is inexpensive — a typical vehicle has at most a few hundred fuel logs.
- **Alternatives considered**:
  - Store MPG in events and emit corrective events when neighbors change → rejected: over-complex, creates event chains
  - Store MPG in events and accept stale data in the event store → rejected: confusing; the event would disagree with the read model
  - Compute MPG at query time (not in projection) → rejected: adds computation to every read; projection is the right place for denormalized read models in Marten
- **Confidence**: High

### D-03: CalculatedField Stored in Events

- **Who**: domain-modeler
- **What**: The `calculatedField` discriminator ("gallons", "totalCost", "pricePerGallon", or null) is stored in the domain event alongside the fuel field values.
- **Why**: The sprint brief specifies that the edit form must show which field was auto-calculated (with a "calc" pill). This is a display concern, but the information must survive a round-trip to the server. Storing it in the event preserves the user's input intent: "I entered gallons and total cost; price/gal was calculated." Without this, the edit form couldn't restore the correct auto-calc state. The value object approach (just storing the three numbers) loses this metadata.
- **Alternatives considered**:
  - Derive which field was calculated by checking mathematical consistency → rejected: all three fields are mathematically consistent by construction, so you can't determine which was the input and which was derived
  - Store this only in the frontend (local storage or state) → rejected: the edit form loads data from the API; it needs this info from the server
- **Confidence**: High

### D-04: Idempotency Pattern Reuses Sprint 02 Approach

- **Who**: domain-modeler
- **What**: `RecordFuelLogCommand` uses a `ProcessedRecordFuelLog` Marten document for server-side idempotency, keyed by `"{userId}::{commandId}"`. Same conceptual pattern as Sprint 02's `ProcessedAddVehicle`.
- **Why**: The sprint brief explicitly requires `commandId` on `RecordFuelLogCommand` for idempotency. The pattern is proven from Sprint 02 and keeps the approach consistent. The composite key `"{userId}::{commandId}"` scopes idempotency per user.
- **Alternatives considered**:
  - Reuse a generic `ProcessedCommand` table for all commands → possible future refactor but premature; each command type has different result data (vehicleId vs fuelLogId)
  - Skip idempotency for fuel logs → rejected: sprint brief explicitly requires it
- **Confidence**: High

### D-05: Value Objects for Validation, Primitives in Events

- **Who**: domain-modeler
- **What**: Value objects (`Odometer`, `FuelAmount`, `FuelCost`, `PricePerGallon`) are used in command handlers for validation logic. Domain events use primitive types (`int`, `decimal`) for their payloads.
- **Why**: Events must be serializable and deserializable across versions without fragility. Using primitives in events gives maximum compatibility — the event store doesn't depend on value object constructors or validation logic. Value objects are used at the handler level to enforce invariants before emitting events. If an event is in the store, the data has already passed validation.
- **Alternatives considered**:
  - Value objects in events with custom JSON converters → rejected: adds serialization complexity; Marten's SystemTextJson works naturally with primitives
  - No value objects (validate with inline checks) → rejected: value objects centralize constraints and make them reusable across handlers
- **Confidence**: High

### D-06: Non-Nullable Fuel Fields in Events (Always All Three Present)

- **Who**: domain-modeler
- **What**: In the persisted events (`FuelLogRecorded`, `FuelLogUpdated`), all three fuel fields (`Gallons`, `TotalCost`, `PricePerGallon`) are non-nullable `decimal`. The command accepts nullable fields (at least two required), but the handler ensures all three are computed before emitting.
- **Why**: This simplifies all downstream consumers (projections, API responses). No projection or read model needs to handle "what if gallons is missing?" — it never is in the event store. The auto-calculation logic lives in `FuelFieldCalculator`, used by both the frontend (for immediate UX) and the server (as a safety net). The server is the authority.
- **Alternatives considered**:
  - Nullable fields in events (match command shape) → rejected: pushes null-handling complexity to every consumer
  - Require all three in the command (no nullable) → rejected: the frontend sends all three after calc, but accepting nullable fields in the command makes the API more forgiving
- **Confidence**: High

### D-07: Full MPG Recalculation on Every Mutation

- **Who**: domain-modeler
- **What**: When any fuel log event is projected (add, update, delete), MPG is recalculated for ALL entries in the list, not just the affected entry and its neighbors.
- **Why**: Editing an entry's date or odometer can change the chronological ordering, which ripples MPG changes across the entire list. Deleting an entry changes the "prior" entry for the one after it. Attempting surgical updates (only recalculate neighbors) is fragile and error-prone. Full recalculation is simple, correct, and fast — a vehicle with 500 fuel logs can be recalculated in microseconds.
- **Alternatives considered**:
  - Surgical recalculation of only affected entries → rejected: fragile; date/odometer changes can reorder entries, making it hard to identify exactly which entries are affected
  - Lazy recalculation at query time → rejected: the read model should be pre-computed for fast reads
- **Confidence**: High

### D-08: Odometer Warning Is Not an Error

- **Who**: domain-modeler
- **What**: If the entered odometer is lower than the vehicle's most recent prior reading, the server does NOT reject the request. It may include a `warning` field in the success response for the frontend to display, but the fill-up is saved.
- **Why**: There are legitimate reasons for a lower odometer: vehicle odometer rollover, correction of a prior entry, backdated fill-up for an earlier date. The sprint brief explicitly states this should be a warning, not an error. The API returns the fill-up's `fuelLogId` along with an optional `odometerWarning` string. The frontend displays the warning but doesn't block saving.
- **Alternatives considered**:
  - Reject as validation error → rejected: sprint brief explicitly says "allow saving anyway"
  - Ignore entirely (no warning) → rejected: the warning helps catch genuine data entry mistakes
- **Confidence**: High

### D-09: FuelLogList Read Model Keyed by "fuellog-{vehicleId}"

- **Who**: domain-modeler
- **What**: The `FuelLogList` read model document is keyed by `"fuellog-{vehicleId}"`, not by the vehicle stream key `"vehicle-{vehicleId}"`.
- **Why**: The vehicle stream already has a `VehicleReadModel` document keyed by the stream identity. Using a different key prefix avoids collisions and makes it clear this is a separate read model. The `fuellog-` prefix is a convention that can extend to other per-vehicle read models (e.g., `maintenance-{vehicleId}` in Sprint 04).
- **Alternatives considered**:
  - Key by `Guid vehicleId` directly (like `VehicleReadModel`) → rejected: would conflict with `VehicleReadModel` which uses `Guid` as document key; different document types can share a key type but it's cleaner to use distinct string keys
  - Key by `"vehicle-{vehicleId}"` (same as stream) → rejected: collides with `VehicleReadModel` which already uses this pattern for its Marten document identity
- **Confidence**: High

### D-10: No Fuel Log Aggregate Root Class

- **Who**: domain-modeler
- **What**: There is no `FuelLog` aggregate class with `Apply()` methods. Fuel log events are appended to the vehicle stream, and the `FuelLogListProjection` directly processes them into the read model.
- **Why**: A FuelLog aggregate would need to track all fuel logs for a vehicle to enforce invariants like "fuel log must exist for update/delete." But the projection's read model already tracks this. Loading a full aggregate from the event stream just to check existence is wasteful — loading the lightweight read model is more efficient. The Vehicle aggregate class doesn't need `Apply` methods for fuel log events because it doesn't track fuel log state. Fuel log handlers use the `FuelLogList` read model for existence and ownership checks.
- **Alternatives considered**:
  - Create a `FuelLog` aggregate class → rejected: it would be a mostly-empty class whose only job is existence tracking, which the read model already does
  - Extend the `Vehicle` aggregate with fuel log state → rejected: bloats the Vehicle aggregate with unrelated state; violates single responsibility
- **Confidence**: High

### D-11: $0.00 Total Cost and $0.000 Price/Gallon Allowed

- **Who**: domain-modeler
- **What**: `TotalCost` of $0.00 and `PricePerGallon` of $0.000 are valid values. `Gallons` must be strictly positive (> 0).
- **Why**: The sprint brief explicitly states "$0.00 total cost — allowed (free fuel: employee perk, promotional, gift card)." A zero total cost naturally produces a zero price/gallon. However, zero gallons makes no sense — you can't have a fill-up with no fuel. The asymmetry (zero cost OK, zero gallons not OK) reflects the real-world semantics.
- **Alternatives considered**:
  - Require all three to be positive → rejected: sprint brief explicitly allows $0.00
  - Allow zero gallons → rejected: a "fill-up" with 0 gallons is not a fill-up
- **Confidence**: High

---

## 10. Sprint Boundary — What's NOT Modeled

| Concept | Sprint | Notes |
|---|---|---|
| Maintenance items & records | Sprint 04 | Separate domain, uses odometer readings from fuel logs |
| Dashboard attention strip / badges | Sprint 05 | Aggregates maintenance status across vehicles |
| Mileage estimation (linear interpolation) | Sprint 05 | Uses fuel log odometer readings as data points |
| Vehicle archive / restore / delete | Sprint 06 | `VehicleArchived`, `VehicleRestored`, cascade to fuel logs |
| Vehicle photos | Sprint 06 | `PhotoUrl` fields |
| Fuel economy charts / trends | Future | Not in any current sprint |
| Average MPG summaries | Future | Not in any current sprint |
| Gas station field | Future | Deferred per "fast entry" principle |
| CSV import/export | Future | Not in any current sprint |
| Kilometers / liters | Future | v1 is miles/gallons only |

---

## 11. Event Catalog Summary

| Event | Stream | Trigger | Sprint |
|---|---|---|---|
| `UserProvisioned` | `user-{userId}` | Gateway auth callback (first login) | 01 |
| `VehicleAdded` | `vehicle-{vehicleId}` | AddVehicleCommand | 02 |
| `VehicleUpdated` | `vehicle-{vehicleId}` | UpdateVehicleCommand | 02 |
| `FuelLogRecorded` | `vehicle-{vehicleId}` | RecordFuelLogCommand (POST /api/v1/vehicles/{vehicleId}/fuel-logs) | **03** |
| `FuelLogUpdated` | `vehicle-{vehicleId}` | UpdateFuelLogCommand (PUT /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}) | **03** |
| `FuelLogDeleted` | `vehicle-{vehicleId}` | DeleteFuelLogCommand (DELETE /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}) | **03** |

## 12. Command Catalog Summary

| Command | Handler | Emits | Caller |
|---|---|---|---|
| `ProvisionUser` | `ProvisionUserHandler` | `UserProvisioned` | Gateway auth callback |
| `AddVehicleCommand` | `AddVehicleHandler` | `VehicleAdded` | Frontend → POST /api/v1/garage/vehicles |
| `UpdateVehicleCommand` | `UpdateVehicleHandler` | `VehicleUpdated` | Frontend → PUT /api/v1/garage/vehicles/{id} |
| `RecordFuelLogCommand` | `RecordFuelLogHandler` | `FuelLogRecorded` | Frontend → POST /api/v1/vehicles/{vehicleId}/fuel-logs |
| `UpdateFuelLogCommand` | `UpdateFuelLogHandler` | `FuelLogUpdated` | Frontend → PUT /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId} |
| `DeleteFuelLogCommand` | `DeleteFuelLogHandler` | `FuelLogDeleted` | Frontend → DELETE /api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId} |

## 13. Projection Summary

| Projection | Type | Lifecycle | Source Events | Read Model | Key Type |
|---|---|---|---|---|---|
| `UserProfileProjection` | SingleStreamProjection | Inline | `UserProvisioned` | `UserProfile` | `string` |
| `VehicleProjection` | SingleStreamProjection | Inline | `VehicleAdded`, `VehicleUpdated` | `VehicleReadModel` | `Guid` |
| `GarageSummaryProjection` | MultiStreamProjection | Inline | `VehicleAdded`, `VehicleUpdated` | `GarageSummary` | `string` |
| `FuelLogListProjection` | MultiStreamProjection | Inline | `FuelLogRecorded`, `FuelLogUpdated`, `FuelLogDeleted` | `FuelLogList` | `string` (`"fuellog-{vehicleId}"`) |
