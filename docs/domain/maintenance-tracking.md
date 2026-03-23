# Domain Model — Sprint 04: Maintenance Tracking

> **Sprint**: maintenance-tracking
> **Modeler**: domain-modeler
> **Date**: 2026-03-23
> **Status**: 🔒 Locked — event catalog finalized before builders begin
> **Depends on**: Sprint 02 (garage-vehicles) — `Vehicle` aggregate, `VehicleReadModel`, string-keyed Marten streams
> **Depends on**: Sprint 03 (fuel-logging) — `FuelLogList` read model provides `LastOdometer` for mileage-based status calculations

---

## Overview

Sprint 04 introduces the **Maintenance** domain — users can define custom maintenance items with mileage and/or time intervals, set warning thresholds, seed an initial baseline, record service events that reset the interval clock, and see at-a-glance OK / Due Soon / Overdue status for each item.

The sprint introduces six domain events (`MaintenanceItemDefined`, `MaintenanceItemUpdated`, `MaintenanceItemRemoved`, `MaintenancePerformed`, `MaintenanceRecordUpdated`, `MaintenanceRecordDeleted`), six commands, one multi-stream projection (`MaintenanceTabProjection`), a status calculation engine (`MaintenanceStatusCalculator`), and a set of value objects for interval and threshold constraints.

**Key modeling decisions in this sprint:**

- Maintenance events live on per-vehicle streams (`vehicle-{vehicleId}`), not separate streams — see Decision D-01.
- Status (OK / Due Soon / Overdue) is computed at query time, not stored in events or projections — see Decision D-02.
- Initial baseline (last performed date/odometer) is stored as properties of the `MaintenanceItemDefined` event, not as a synthetic service record — see Decision D-03.
- The `MaintenanceTabData` read model uses a separate key prefix (`maintenance-{vehicleId}`) — see Decision D-04.
- Idempotency for `DefineMaintenanceItemCommand` and `RecordMaintenanceCommand` uses the same `ProcessedCommand` pattern as prior sprints — see Decision D-05.
- The `MaintenancePerformed` event includes `UserId` for ownership verification and projection routing — see Decision D-06.

---

## 1. Aggregates & Entities

### MaintenanceItem (Not a Standalone Aggregate — Events on Vehicle Stream)

Maintenance events are appended to the **vehicle stream** (`vehicle-{vehicleId}`), not to their own independent streams. A `MaintenanceItem` is a domain entity within the vehicle's event history, exactly like `FuelLog` in Sprint 03. See Decision D-01 for rationale.

The Vehicle aggregate is not directly modified — maintenance events are additional event types on the same stream, consumed by a dedicated projection (`MaintenanceTabProjection`) to build the maintenance read model.

**MaintenanceItem entity fields** (as stored in events):

| Property | Type | Description |
|---|---|---|
| **ItemId** | `Guid` | Unique identity for this maintenance item. Generated server-side. |
| **VehicleId** | `Guid` | The vehicle this item belongs to. From URL/command. |
| **UserId** | `string` | WorkOS user ID. Denormalized for ownership verification & projection routing. |
| **Name** | `string` | Display name, e.g. "Oil Change". 1–100 chars, trimmed. |
| **MileageInterval** | `int?` | Miles between services. Positive integer, max 999,999. Null if time-only. |
| **TimeIntervalMonths** | `int?` | Months between services. 1–120. Null if mileage-only. |
| **MileageWarning** | `int` | Warn this many miles before due. Default 500. Must be < mileage interval. |
| **TimeWarningDays** | `int` | Warn this many days before due. Default 14. Must be < interval in days. |
| **InitialLastPerformedDate** | `LocalDate?` | Seed baseline: when last done. Only on creation. |
| **InitialLastPerformedOdometer** | `int?` | Seed baseline: odometer at last service. Only on creation. |
| **OccurredAt** | `Instant` | Server timestamp when the event was created. |

**Stream ID**: `vehicle-{vehicleId}` (shared with Vehicle aggregate and FuelLog events)

**Invariants**:

1. `ItemId` must be a non-empty Guid (generated server-side).
2. `VehicleId` must reference an existing vehicle owned by the authenticated user.
3. `Name` must be non-blank after trim, 1–100 characters.
4. At least one of {`MileageInterval`, `TimeIntervalMonths`} must be set.
5. `MileageInterval` (if set): positive integer, max 999,999.
6. `TimeIntervalMonths` (if set): positive integer, 1–120.
7. `MileageWarning` (if `MileageInterval` set): positive integer, must be < `MileageInterval`.
8. `TimeWarningDays` (if `TimeIntervalMonths` set): positive integer, must be < `TimeIntervalMonths` × 30.44 (converted to days).
9. `InitialLastPerformedDate` (if set): must not be in the future.
10. `InitialLastPerformedOdometer` (if set): positive integer, max 9,999,999.
11. Duplicate item names are allowed per vehicle (see Sprint Brief: "Tire Rotation — Front" and "Tire Rotation — Rear").

### MaintenanceRecord (Sub-Entity of MaintenanceItem — Events on Vehicle Stream)

A service record documents that maintenance was performed. Records belong to a specific `MaintenanceItem` and live on the same vehicle stream.

**MaintenanceRecord entity fields** (as stored in events):

| Property | Type | Description |
|---|---|---|
| **RecordId** | `Guid` | Unique identity for this service record. Generated server-side. |
| **ItemId** | `Guid` | The maintenance item this record belongs to. |
| **VehicleId** | `Guid` | The vehicle (for stream routing). |
| **UserId** | `string` | WorkOS user ID. Denormalized for ownership verification. |
| **Date** | `LocalDate` | Date the service was performed. Not future. |
| **Odometer** | `int` | Odometer reading at service. Positive, max 9,999,999. |
| **Cost** | `decimal?` | Service cost. Non-negative, max 99,999.99, ≤ 2 dp. $0 allowed. |
| **Notes** | `string?` | Free-text notes. Max 500 chars. |
| **OccurredAt** | `Instant` | Server timestamp when the event was created. |

**Invariants**:

1. `RecordId` must be a non-empty Guid (generated server-side).
2. `ItemId` must reference an existing, non-removed maintenance item on this vehicle.
3. `VehicleId` must reference an existing vehicle owned by the authenticated user.
4. `Date` must not be in the future.
5. `Odometer` must be a positive integer ≤ 9,999,999.
6. `Cost` (if provided): non-negative decimal, ≤ 2 decimal places, max 99,999.99. $0.00 valid (DIY maintenance).
7. `Notes` (if provided): max 500 characters.

---

## 2. Value Objects

This sprint introduces value objects for maintenance-specific domain types. The `Odometer` value object from Sprint 03 is reused.

### MileageInterval

Represents the number of miles between maintenance services.

```csharp
public readonly record struct MileageInterval
{
    public const int Max = 999_999;
    public int Value { get; }

    public MileageInterval(int value)
    {
        if (value is <= 0 or > Max)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Mileage interval must be a positive integer ≤ 999,999.");
        Value = value;
    }

    public static implicit operator int(MileageInterval m) => m.Value;
}
```

**Constraints**: Positive integer, max 999,999.

### TimeInterval

Represents the number of months between maintenance services.

```csharp
public readonly record struct TimeInterval
{
    public const int MaxMonths = 120;
    public int Months { get; }

    public TimeInterval(int months)
    {
        if (months is <= 0 or > MaxMonths)
            throw new ArgumentOutOfRangeException(nameof(months),
                "Time interval must be 1–120 months.");
        Months = months;
    }

    public double ToDays() => Months * 30.44;

    public static implicit operator int(TimeInterval t) => t.Months;
}
```

**Constraints**: Positive integer, 1–120 months. Conversion to days uses 30.44 (average days/month).

### MileageWarningThreshold

Represents the number of miles before due to start warning.

```csharp
public readonly record struct MileageWarningThreshold
{
    public const int Default = 500;
    public int Value { get; }

    public MileageWarningThreshold(int value, int mileageInterval)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Warning threshold must be positive.");
        if (value >= mileageInterval)
            throw new ArgumentException(
                "Warning threshold must be less than the interval.", nameof(value));
        Value = value;
    }

    public static implicit operator int(MileageWarningThreshold m) => m.Value;
}
```

**Constraints**: Positive integer. Must be strictly less than the associated `MileageInterval`.

### TimeWarningThreshold

Represents the number of days before due to start warning.

```csharp
public readonly record struct TimeWarningThreshold
{
    public const int DefaultDays = 14;
    public int Days { get; }

    public TimeWarningThreshold(int days, int timeIntervalMonths)
    {
        if (days <= 0)
            throw new ArgumentOutOfRangeException(nameof(days),
                "Warning threshold must be positive.");
        var intervalDays = timeIntervalMonths * 30.44;
        if (days >= intervalDays)
            throw new ArgumentException(
                "Warning threshold must be less than the interval.", nameof(days));
        Days = days;
    }

    public static implicit operator int(TimeWarningThreshold t) => t.Days;
}
```

**Constraints**: Positive integer (days). Must be strictly less than the associated `TimeIntervalMonths` × 30.44 (converted to days).

### MaintenanceCost

Represents the cost of a maintenance service.

```csharp
public readonly record struct MaintenanceCost
{
    public decimal Value { get; }

    public MaintenanceCost(decimal value)
    {
        if (value < 0m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Cost must be non-negative.");
        if (value > 99_999.99m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Cost must be ≤ 99,999.99.");
        if (decimal.Round(value, 2) != value)
            throw new ArgumentException(
                "Cost allows at most 2 decimal places.", nameof(value));
        Value = value;
    }
}
```

**Constraints**: Non-negative (≥ $0.00 — DIY maintenance allowed), ≤ 2 decimal places, max 99,999.99.

### MaintenanceStatus (String Constants)

Status discriminator for maintenance item urgency.

```csharp
public static class MaintenanceStatus
{
    public const string Ok = "ok";
    public const string DueSoon = "dueSoon";
    public const string Overdue = "overdue";

    public static int SortOrder(string status) => status switch
    {
        Overdue => 0,
        DueSoon => 1,
        Ok => 2,
        _ => 3,
    };
}
```

**Rationale**: Using `string` constants rather than a C# `enum` for stable JSON serialization, consistent with `CalculatedFieldKind` from Sprint 03.

### Reused Value Objects

- **`Odometer`** — from Sprint 03. Same constraints: positive integer ≤ 9,999,999.

---

## 3. Event Catalog

### MaintenanceItemDefined

Records that a user has created a new maintenance item for a vehicle.

```
Event:       MaintenanceItemDefined
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle, FuelLog events)

Payload:
  - ItemId                       : Guid      — New item identity. Generated server-side.
  - VehicleId                    : Guid      — The vehicle this item belongs to.
  - UserId                       : string    — WorkOS user ID. Denormalized for ownership/routing.
  - CommandId                    : Guid      — Client-generated idempotency key.
  - Name                         : string    — Display name. 1–100 chars, trimmed.
  - MileageInterval              : int?      — Miles between services. Null if time-only.
  - TimeIntervalMonths           : int?      — Months between services. Null if mileage-only.
  - MileageWarning               : int       — Miles before due to warn. Default 500.
  - TimeWarningDays              : int       — Days before due to warn. Default 14.
  - InitialLastPerformedDate     : LocalDate? — Seed baseline date. Null if unknown.
  - InitialLastPerformedOdometer : int?      — Seed baseline odometer. Null if unknown.
  - OccurredAt                   : Instant   — NodaTime server timestamp.

Invariants:
  - ItemId must be a non-empty Guid
  - VehicleId must reference an existing vehicle owned by the authenticated user
  - CommandId must be a non-empty Guid (unique per user)
  - Name must be 1–100 chars, non-blank after trim
  - At least one of {MileageInterval, TimeIntervalMonths} must be non-null
  - MileageInterval (if set): positive integer ≤ 999,999
  - TimeIntervalMonths (if set): positive integer, 1–120
  - MileageWarning: positive, must be < MileageInterval (when MileageInterval is set)
  - TimeWarningDays: positive, must be < TimeIntervalMonths × 30.44 (when TimeIntervalMonths is set)
  - InitialLastPerformedDate (if set): must not be in the future
  - InitialLastPerformedOdometer (if set): positive integer ≤ 9,999,999

Trigger:
  - Frontend POST to define-maintenance-item endpoint with DefineMaintenanceItemCommand payload
```

**C# record definition**:

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

### MaintenanceItemUpdated

Records that a user has modified a maintenance item's definition (name, intervals, thresholds). Does NOT modify initial baseline — that is immutable after creation.

```
Event:       MaintenanceItemUpdated
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle, FuelLog events)

Payload:
  - ItemId             : Guid   — The item being updated.
  - VehicleId          : Guid   — The vehicle this item belongs to.
  - UserId             : string — WorkOS user ID. Denormalized for projection routing.
  - Name               : string — Updated display name. 1–100 chars, trimmed.
  - MileageInterval    : int?   — Updated mileage interval.
  - TimeIntervalMonths : int?   — Updated time interval.
  - MileageWarning     : int    — Updated mileage warning threshold.
  - TimeWarningDays    : int    — Updated time warning threshold.
  - OccurredAt         : Instant — NodaTime server timestamp.

Invariants:
  - ItemId must reference an existing, non-removed maintenance item on this vehicle
  - VehicleId must match the stream's vehicle
  - UserId must match the vehicle's owner (ownership check)
  - Same validation rules as MaintenanceItemDefined for name, intervals, and thresholds

Trigger:
  - Frontend PUT to update-maintenance-item endpoint with UpdateMaintenanceItemCommand payload
```

**C# record definition**:

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

**Note**: `UserId` is included for projection routing (same rationale as `VehicleUpdated` including `UserId` per Decision D-07 in Sprint 02). The `MaintenanceTabProjection` is keyed by vehicle ID, and `UserId` enables the projection to maintain `UserId` on the read model without a separate lookup.

### MaintenanceItemRemoved

Records that a user has removed a maintenance item and all its service history.

```
Event:       MaintenanceItemRemoved
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle, FuelLog events)

Payload:
  - ItemId      : Guid    — The item being removed.
  - VehicleId   : Guid    — The vehicle this item belongs to.
  - UserId      : string  — WorkOS user ID. Denormalized for ownership verification.
  - OccurredAt  : Instant — NodaTime server timestamp.

Invariants:
  - ItemId must reference an existing, non-removed maintenance item on this vehicle
  - VehicleId must match the stream's vehicle
  - UserId must match the vehicle's owner

Trigger:
  - Frontend DELETE to remove-maintenance-item endpoint with RemoveMaintenanceItemCommand payload
```

**C# record definition**:

```csharp
public sealed record MaintenanceItemRemoved(
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Instant OccurredAt
);
```

### MaintenancePerformed

Records that a user has logged a service for a maintenance item. This is the primary event that "resets the clock."

```
Event:       MaintenancePerformed
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle, FuelLog events)

Payload:
  - RecordId    : Guid      — New service record identity. Generated server-side.
  - ItemId      : Guid      — The maintenance item being serviced.
  - VehicleId   : Guid      — The vehicle.
  - UserId      : string    — WorkOS user ID. Denormalized for ownership/routing.
  - CommandId   : Guid      — Client-generated idempotency key.
  - Date        : LocalDate — Date the service was performed. Not future.
  - Odometer    : int       — Odometer at service. Positive, ≤ 9,999,999.
  - Cost        : decimal?  — Service cost. Non-negative, ≤ 2 dp, max 99,999.99.
  - Notes       : string?   — Free-text notes. Max 500 chars.
  - OccurredAt  : Instant   — NodaTime server timestamp.

Invariants:
  - RecordId must be a non-empty Guid
  - ItemId must reference an existing, non-removed maintenance item on this vehicle
  - VehicleId must reference an existing vehicle owned by the authenticated user
  - CommandId must be a non-empty Guid (unique per user)
  - Date must not be in the future
  - Odometer must be a positive integer ≤ 9,999,999
  - Cost (if present): non-negative, ≤ 2 dp, max 99,999.99
  - Notes (if present): max 500 chars

Trigger:
  - Frontend POST to record-maintenance endpoint with RecordMaintenanceCommand payload
```

**C# record definition**:

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

### MaintenanceRecordUpdated

Records that a user has modified an existing service record.

```
Event:       MaintenanceRecordUpdated
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle, FuelLog events)

Payload:
  - RecordId    : Guid      — The service record being updated.
  - ItemId      : Guid      — The maintenance item this record belongs to.
  - VehicleId   : Guid      — The vehicle.
  - UserId      : string    — WorkOS user ID. Denormalized for ownership verification.
  - Date        : LocalDate — Updated service date.
  - Odometer    : int       — Updated odometer.
  - Cost        : decimal?  — Updated cost.
  - Notes       : string?   — Updated notes.
  - OccurredAt  : Instant   — NodaTime server timestamp.

Invariants:
  - RecordId must reference an existing service record on this maintenance item
  - ItemId must reference an existing, non-removed maintenance item on this vehicle
  - VehicleId must match the stream's vehicle
  - UserId must match the vehicle's owner
  - Same validation rules as MaintenancePerformed for date, odometer, cost, notes

Trigger:
  - Frontend PUT to update-maintenance-record endpoint with UpdateMaintenanceRecordCommand payload
```

**C# record definition**:

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

### MaintenanceRecordDeleted

Records that a user has removed a service record. When the last record is deleted, the item falls back to the initial baseline (if set), or to "never performed" status.

```
Event:       MaintenanceRecordDeleted
Stream:      vehicle-{vehicleId}
Stream Type: string-keyed (shared with Vehicle, FuelLog events)

Payload:
  - RecordId    : Guid    — The service record being deleted.
  - ItemId      : Guid    — The maintenance item this record belongs to.
  - VehicleId   : Guid    — The vehicle.
  - UserId      : string  — WorkOS user ID. Denormalized for ownership verification.
  - OccurredAt  : Instant — NodaTime server timestamp.

Invariants:
  - RecordId must reference an existing service record on this maintenance item
  - ItemId must reference an existing, non-removed maintenance item on this vehicle
  - VehicleId must match the stream's vehicle
  - UserId must match the vehicle's owner

Trigger:
  - Frontend DELETE to delete-maintenance-record endpoint with DeleteMaintenanceRecordCommand payload
```

**C# record definition**:

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

## 4. Command Catalog

### DefineMaintenanceItemCommand

Creates a new maintenance item for a vehicle. Idempotent via `commandId`.

```
Command:     DefineMaintenanceItemCommand
Handler:     DefineMaintenanceItemHandler (static class, Wolverine command handler)
Emits:       MaintenanceItemDefined (only if commandId has not been processed)
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - CommandId                    : Guid      — Client-generated UUID for idempotency
  - VehicleId                    : Guid      — Target vehicle (from URL route)
  - Name                         : string    — Display name
  - MileageInterval              : int?      — Miles between services
  - TimeIntervalMonths           : int?      — Months between services
  - MileageWarning               : int       — Miles before due to warn (default 500 if not specified)
  - TimeWarningDays              : int       — Days before due to warn (default 14 if not specified)
  - InitialLastPerformedDate     : LocalDate? — Seed baseline date
  - InitialLastPerformedOdometer : int?      — Seed baseline odometer

Validation:
  - CommandId required, non-empty Guid
  - VehicleId required, must reference an existing vehicle owned by the authenticated user
  - Name required, 1–100 chars, non-blank after trim. Error: "Give this item a name"
  - At least one of {MileageInterval, TimeIntervalMonths} required. Error: "Set a mileage interval, time interval, or both"
  - MileageInterval (if set): positive integer, max 999,999. Error: "Enter a valid mileage interval"
  - TimeIntervalMonths (if set): positive integer, 1–120. Error: "Enter a valid time interval (1–120 months)"
  - MileageWarning (if MileageInterval set): positive integer, < MileageInterval. Error: "Warning threshold must be less than the interval"
  - TimeWarningDays (if TimeIntervalMonths set): positive integer, < interval in days. Error: "Warning threshold must be less than the interval"
  - InitialLastPerformedDate (if set): not in the future. Error: "Date can't be in the future"
  - InitialLastPerformedOdometer (if set): positive integer ≤ 9,999,999. Error: "Enter a valid odometer reading"

Idempotency:
  - Handler queries for an existing ProcessedDefineMaintenanceItem document keyed by (UserId, CommandId)
  - If found, returns the previously created ItemId (no event emitted, no error)
  - If not found, generates a new ItemId, emits MaintenanceItemDefined, stores idempotency marker

Caller:
  - Frontend via POST /api/v1/vehicles/{vehicleId}/maintenance/items
```

**C# record definition**:

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

**Handler sketch**:

```csharp
public static class DefineMaintenanceItemHandler
{
    public static async Task<DefineMaintenanceItemResult> Handle(
        DefineMaintenanceItemCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Validate CommandId
        if (command.CommandId == Guid.Empty)
            throw new MaintenanceValidationException(/* commandId required */);

        // 2. Idempotency check
        var markerId = ProcessedCommandId.From(auth.UserId, command.CommandId);
        var processed = await session.LoadAsync<ProcessedDefineMaintenanceItem>(markerId, ct);
        if (processed is not null)
            return new DefineMaintenanceItemResult(processed.ItemId, IsDuplicate: true);

        // 3. Ownership check: load vehicle read model
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 4. Validate & normalize fields
        MaintenanceItemInput.NormalizeAndValidate(
            command.Name, command.MileageInterval, command.TimeIntervalMonths,
            command.MileageWarning, command.TimeWarningDays,
            command.InitialLastPerformedDate, command.InitialLastPerformedOdometer,
            clock);

        // 5. Emit event
        var itemId = Guid.NewGuid();
        var now = clock.GetCurrentInstant();

        var defined = new MaintenanceItemDefined(
            itemId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            command.Name.Trim(),
            command.MileageInterval,
            command.TimeIntervalMonths,
            command.MileageWarning,
            command.TimeWarningDays,
            command.InitialLastPerformedDate,
            command.InitialLastPerformedOdometer,
            now);

        session.Events.Append(vehicleStreamId, defined);

        // 6. Store idempotency marker
        session.Insert(new ProcessedDefineMaintenanceItem(
            markerId, auth.UserId, itemId, now));

        await session.SaveChangesAsync(ct);
        return new DefineMaintenanceItemResult(itemId, IsDuplicate: false);
    }
}
```

### UpdateMaintenanceItemCommand

Updates an existing maintenance item's definition. No idempotency key — PUT semantics. Does NOT modify the initial baseline.

```
Command:     UpdateMaintenanceItemCommand
Handler:     UpdateMaintenanceItemHandler (static class, Wolverine command handler)
Emits:       MaintenanceItemUpdated
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - ItemId             : Guid   — The item to update (from URL route)
  - VehicleId          : Guid   — The vehicle (from URL route)
  - Name               : string — Updated display name
  - MileageInterval    : int?   — Updated mileage interval
  - TimeIntervalMonths : int?   — Updated time interval
  - MileageWarning     : int    — Updated mileage warning
  - TimeWarningDays    : int    — Updated time warning

Validation:
  - Same rules as DefineMaintenanceItemCommand for name, intervals, and thresholds (no initial state fields)
  - ItemId required, must reference an existing non-removed maintenance item on this vehicle
  - VehicleId required, vehicle must exist and belong to authenticated user

Access control:
  - Handler loads MaintenanceTabData read model and verifies:
    1. Vehicle exists and belongs to authenticated user
    2. ItemId exists in the vehicle's maintenance items (not removed)
  - If not found → 404

Caller:
  - Frontend via PUT /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}
```

**C# record definition**:

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

**Handler sketch**:

```csharp
public static class UpdateMaintenanceItemHandler
{
    public static async Task Handle(
        UpdateMaintenanceItemCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Ownership check
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 2. Verify item exists
        var tabData = await session.LoadAsync<MaintenanceTabData>(
            MaintenanceTabDataId.From(command.VehicleId), ct);
        if (tabData is null || !tabData.Items.Any(i => i.ItemId == command.ItemId))
            throw new MaintenanceItemNotFoundException(command.ItemId);

        // 3. Validate & normalize (no initial state fields)
        MaintenanceItemInput.NormalizeAndValidate(
            command.Name, command.MileageInterval, command.TimeIntervalMonths,
            command.MileageWarning, command.TimeWarningDays,
            initialDate: null, initialOdometer: null, clock);

        // 4. Emit event
        var now = clock.GetCurrentInstant();
        session.Events.Append(vehicleStreamId, new MaintenanceItemUpdated(
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            command.Name.Trim(),
            command.MileageInterval,
            command.TimeIntervalMonths,
            command.MileageWarning,
            command.TimeWarningDays,
            now));

        await session.SaveChangesAsync(ct);
    }
}
```

### RemoveMaintenanceItemCommand

Removes a maintenance item and all its service history. Uses confirmation dialog on frontend.

```
Command:     RemoveMaintenanceItemCommand
Handler:     RemoveMaintenanceItemHandler (static class, Wolverine command handler)
Emits:       MaintenanceItemRemoved
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - ItemId    : Guid — The item to remove (from URL route)
  - VehicleId : Guid — The vehicle (from URL route)

Validation:
  - ItemId required, must reference an existing non-removed maintenance item
  - VehicleId required, vehicle must exist and belong to authenticated user

Access control:
  - Same as UpdateMaintenanceItemCommand

Caller:
  - Frontend via DELETE /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}
```

**C# record definition**:

```csharp
public sealed record RemoveMaintenanceItemCommand(
    Guid ItemId,
    Guid VehicleId
);
```

**Handler sketch**:

```csharp
public static class RemoveMaintenanceItemHandler
{
    public static async Task Handle(
        RemoveMaintenanceItemCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Ownership check
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 2. Verify item exists
        var tabData = await session.LoadAsync<MaintenanceTabData>(
            MaintenanceTabDataId.From(command.VehicleId), ct);
        if (tabData is null || !tabData.Items.Any(i => i.ItemId == command.ItemId))
            throw new MaintenanceItemNotFoundException(command.ItemId);

        // 3. Emit removal event
        var now = clock.GetCurrentInstant();
        session.Events.Append(vehicleStreamId, new MaintenanceItemRemoved(
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            now));

        await session.SaveChangesAsync(ct);
    }
}
```

### RecordMaintenanceCommand

Records that maintenance was performed. Resets the interval clock. Idempotent via `commandId`.

```
Command:     RecordMaintenanceCommand
Handler:     RecordMaintenanceHandler (static class, Wolverine command handler)
Emits:       MaintenancePerformed (only if commandId has not been processed)
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - CommandId : Guid      — Client-generated UUID for idempotency
  - ItemId    : Guid      — The maintenance item being serviced
  - VehicleId : Guid      — Target vehicle (from URL route)
  - Date      : LocalDate — Date of service
  - Odometer  : int       — Odometer at service
  - Cost      : decimal?  — Service cost
  - Notes     : string?   — Free-text notes

Validation:
  - CommandId required, non-empty Guid
  - ItemId required, must reference an existing non-removed maintenance item on this vehicle
  - VehicleId required, must reference an existing vehicle owned by the authenticated user
  - Date required, valid LocalDate, not in the future. Error: "Date can't be in the future"
  - Odometer required, positive integer ≤ 9,999,999. Error: "Enter a valid odometer reading"
  - Cost (if present): non-negative, ≤ 2 dp, max 99,999.99. Error: "Enter a valid cost"
  - Notes (if present): max 500 chars. Error: "Notes are too long"

Idempotency:
  - Handler queries for an existing ProcessedRecordMaintenance document keyed by (UserId, CommandId)
  - If found, returns the previously created RecordId (no event emitted, no error)
  - If not found, generates a new RecordId, emits MaintenancePerformed, stores idempotency marker

Caller:
  - Frontend via POST /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}/records
```

**C# record definition**:

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

**Handler sketch**:

```csharp
public static class RecordMaintenanceHandler
{
    public static async Task<RecordMaintenanceResult> Handle(
        RecordMaintenanceCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Validate CommandId
        if (command.CommandId == Guid.Empty)
            throw new MaintenanceValidationException(/* commandId required */);

        // 2. Idempotency check
        var markerId = ProcessedCommandId.From(auth.UserId, command.CommandId);
        var processed = await session.LoadAsync<ProcessedRecordMaintenance>(markerId, ct);
        if (processed is not null)
            return new RecordMaintenanceResult(processed.RecordId, IsDuplicate: true);

        // 3. Ownership check
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 4. Verify item exists
        var tabData = await session.LoadAsync<MaintenanceTabData>(
            MaintenanceTabDataId.From(command.VehicleId), ct);
        if (tabData is null || !tabData.Items.Any(i => i.ItemId == command.ItemId))
            throw new MaintenanceItemNotFoundException(command.ItemId);

        // 5. Validate fields
        MaintenanceRecordInput.Validate(
            command.Date, command.Odometer, command.Cost, command.Notes, clock);

        // 6. Emit event
        var recordId = Guid.NewGuid();
        var now = clock.GetCurrentInstant();

        var performed = new MaintenancePerformed(
            recordId,
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            command.Date,
            command.Odometer,
            command.Cost,
            command.Notes?.Trim(),
            now);

        session.Events.Append(vehicleStreamId, performed);

        // 7. Store idempotency marker
        session.Insert(new ProcessedRecordMaintenance(
            markerId, auth.UserId, recordId, now));

        await session.SaveChangesAsync(ct);
        return new RecordMaintenanceResult(recordId, IsDuplicate: false);
    }
}
```

### UpdateMaintenanceRecordCommand

Updates an existing service record. No idempotency key — PUT semantics.

```
Command:     UpdateMaintenanceRecordCommand
Handler:     UpdateMaintenanceRecordHandler (static class, Wolverine command handler)
Emits:       MaintenanceRecordUpdated
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - RecordId  : Guid      — The record to update (from URL route)
  - ItemId    : Guid      — The maintenance item (from URL route)
  - VehicleId : Guid      — The vehicle (from URL route)
  - Date      : LocalDate — Updated service date
  - Odometer  : int       — Updated odometer
  - Cost      : decimal?  — Updated cost
  - Notes     : string?   — Updated notes

Validation:
  - Same rules as RecordMaintenanceCommand for date, odometer, cost, notes (except no CommandId)
  - RecordId required, must reference an existing service record on this item
  - ItemId required, must reference an existing non-removed maintenance item
  - VehicleId required, vehicle must exist and belong to authenticated user

Access control:
  - Handler loads MaintenanceTabData and verifies:
    1. Vehicle exists and belongs to authenticated user
    2. ItemId exists (not removed)
    3. RecordId exists within the item's records
  - If not found → 404

Caller:
  - Frontend via PUT /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}
```

**C# record definition**:

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

**Handler sketch**:

```csharp
public static class UpdateMaintenanceRecordHandler
{
    public static async Task Handle(
        UpdateMaintenanceRecordCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Ownership check
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 2. Verify item and record exist
        var tabData = await session.LoadAsync<MaintenanceTabData>(
            MaintenanceTabDataId.From(command.VehicleId), ct);
        var item = tabData?.Items.FirstOrDefault(i => i.ItemId == command.ItemId);
        if (item is null)
            throw new MaintenanceItemNotFoundException(command.ItemId);
        if (!item.Records.Any(r => r.RecordId == command.RecordId))
            throw new MaintenanceRecordNotFoundException(command.RecordId);

        // 3. Validate fields
        MaintenanceRecordInput.Validate(
            command.Date, command.Odometer, command.Cost, command.Notes, clock);

        // 4. Emit event
        var now = clock.GetCurrentInstant();
        session.Events.Append(vehicleStreamId, new MaintenanceRecordUpdated(
            command.RecordId,
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            command.Date,
            command.Odometer,
            command.Cost,
            command.Notes?.Trim(),
            now));

        await session.SaveChangesAsync(ct);
    }
}
```

### DeleteMaintenanceRecordCommand

Deletes a service record. Status recalculates based on the next most recent record.

```
Command:     DeleteMaintenanceRecordCommand
Handler:     DeleteMaintenanceRecordHandler (static class, Wolverine command handler)
Emits:       MaintenanceRecordDeleted
Stream:      vehicle-{vehicleId} (existing stream, append)

Payload:
  - RecordId  : Guid — The record to delete (from URL route)
  - ItemId    : Guid — The maintenance item (from URL route)
  - VehicleId : Guid — The vehicle (from URL route)

Validation:
  - RecordId required, must reference an existing service record on this item
  - ItemId required, must reference an existing non-removed maintenance item
  - VehicleId required, vehicle must exist and belong to authenticated user

Access control:
  - Same as UpdateMaintenanceRecordCommand

Caller:
  - Frontend via DELETE /api/v1/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}
```

**C# record definition**:

```csharp
public sealed record DeleteMaintenanceRecordCommand(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId
);
```

**Handler sketch**:

```csharp
public static class DeleteMaintenanceRecordHandler
{
    public static async Task Handle(
        DeleteMaintenanceRecordCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken ct)
    {
        // 1. Ownership check
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, ct);
        if (vehicle is null || vehicle.UserId != auth.UserId)
            throw new VehicleNotFoundException(command.VehicleId);

        // 2. Verify item and record exist
        var tabData = await session.LoadAsync<MaintenanceTabData>(
            MaintenanceTabDataId.From(command.VehicleId), ct);
        var item = tabData?.Items.FirstOrDefault(i => i.ItemId == command.ItemId);
        if (item is null)
            throw new MaintenanceItemNotFoundException(command.ItemId);
        if (!item.Records.Any(r => r.RecordId == command.RecordId))
            throw new MaintenanceRecordNotFoundException(command.RecordId);

        // 3. Emit deletion event
        var now = clock.GetCurrentInstant();
        session.Events.Append(vehicleStreamId, new MaintenanceRecordDeleted(
            command.RecordId,
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            now));

        await session.SaveChangesAsync(ct);
    }
}
```

---

## 5. Projections

### MaintenanceTabProjection (Multi-Stream)

Builds the `MaintenanceTabData` read model from maintenance events across vehicle streams. Keyed by vehicle ID. Uses `MultiStreamProjection` with `Inline` lifecycle for immediate read-after-write consistency.

```
Projection:    MaintenanceTabProjection
Type:          MultiStreamProjection<MaintenanceTabData, string>
Lifecycle:     Inline
Source Events: MaintenanceItemDefined, MaintenanceItemUpdated, MaintenanceItemRemoved,
               MaintenancePerformed, MaintenanceRecordUpdated, MaintenanceRecordDeleted,
               FuelLogRecorded, FuelLogUpdated, FuelLogDeleted
```

**Why subscribe to FuelLog events?** The `MaintenanceTabData` read model tracks `LastOdometer` — the most recent odometer reading from *any* source (fuel logs or maintenance records). This is used for mileage-based status calculations. See Decision D-07.

**Read Model: `MaintenanceTabData`**

```csharp
public sealed class MaintenanceTabData
{
    public string Id { get; set; } = string.Empty;           // "maintenance-{vehicleId}"
    public Guid VehicleId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<MaintenanceItemEntry> Items { get; set; } = [];
    public int? LastOdometer { get; set; }                    // Most recent odometer from any source
}
```

**Read Model: `MaintenanceItemEntry`**

```csharp
public sealed class MaintenanceItemEntry
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MileageInterval { get; set; }
    public int? TimeIntervalMonths { get; set; }
    public int MileageWarning { get; set; }
    public int TimeWarningDays { get; set; }
    public LocalDate? InitialLastPerformedDate { get; set; }
    public int? InitialLastPerformedOdometer { get; set; }
    public List<MaintenanceRecordEntry> Records { get; set; } = [];
    public Instant CreatedAt { get; set; }
}
```

**Read Model: `MaintenanceRecordEntry`**

```csharp
public sealed class MaintenanceRecordEntry
{
    public Guid RecordId { get; set; }
    public Guid ItemId { get; set; }
    public LocalDate Date { get; set; }
    public int Odometer { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public Instant CreatedAt { get; set; }
}
```

**Projection implementation sketch**:

```csharp
public sealed class MaintenanceTabProjection : MultiStreamProjection<MaintenanceTabData, string>
{
    public MaintenanceTabProjection()
    {
        Lifecycle = ProjectionLifecycle.Inline;

        // Route maintenance events to the maintenance-{vehicleId} document
        Identity<MaintenanceItemDefined>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenanceItemUpdated>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenanceItemRemoved>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenancePerformed>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenanceRecordUpdated>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenanceRecordDeleted>(e => MaintenanceTabDataId.From(e.VehicleId));

        // Route fuel log events (for LastOdometer tracking)
        Identity<FuelLogRecorded>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<FuelLogUpdated>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<FuelLogDeleted>(e => MaintenanceTabDataId.From(e.VehicleId));

        ProjectEvent<MaintenanceItemDefined>((data, e) =>
        {
            data.VehicleId = e.VehicleId;
            data.UserId = e.UserId;

            data.Items.Add(new MaintenanceItemEntry
            {
                ItemId = e.ItemId,
                Name = e.Name,
                MileageInterval = e.MileageInterval,
                TimeIntervalMonths = e.TimeIntervalMonths,
                MileageWarning = e.MileageWarning,
                TimeWarningDays = e.TimeWarningDays,
                InitialLastPerformedDate = e.InitialLastPerformedDate,
                InitialLastPerformedOdometer = e.InitialLastPerformedOdometer,
                CreatedAt = e.OccurredAt,
            });
        });

        ProjectEvent<MaintenanceItemUpdated>((data, e) =>
        {
            var item = data.Items.FirstOrDefault(i => i.ItemId == e.ItemId);
            if (item is not null)
            {
                item.Name = e.Name;
                item.MileageInterval = e.MileageInterval;
                item.TimeIntervalMonths = e.TimeIntervalMonths;
                item.MileageWarning = e.MileageWarning;
                item.TimeWarningDays = e.TimeWarningDays;
            }
        });

        ProjectEvent<MaintenanceItemRemoved>((data, e) =>
        {
            data.Items.RemoveAll(i => i.ItemId == e.ItemId);
        });

        ProjectEvent<MaintenancePerformed>((data, e) =>
        {
            var item = data.Items.FirstOrDefault(i => i.ItemId == e.ItemId);
            if (item is not null)
            {
                item.Records.Add(new MaintenanceRecordEntry
                {
                    RecordId = e.RecordId,
                    ItemId = e.ItemId,
                    Date = e.Date,
                    Odometer = e.Odometer,
                    Cost = e.Cost,
                    Notes = e.Notes,
                    CreatedAt = e.OccurredAt,
                });

                // Sort records reverse-chronologically for display
                item.Records = item.Records
                    .OrderByDescending(r => r.Date)
                    .ThenByDescending(r => r.Odometer)
                    .ToList();
            }

            RecalculateLastOdometer(data);
        });

        ProjectEvent<MaintenanceRecordUpdated>((data, e) =>
        {
            var item = data.Items.FirstOrDefault(i => i.ItemId == e.ItemId);
            var record = item?.Records.FirstOrDefault(r => r.RecordId == e.RecordId);
            if (record is not null)
            {
                record.Date = e.Date;
                record.Odometer = e.Odometer;
                record.Cost = e.Cost;
                record.Notes = e.Notes;

                // Re-sort
                item!.Records = item.Records
                    .OrderByDescending(r => r.Date)
                    .ThenByDescending(r => r.Odometer)
                    .ToList();
            }

            RecalculateLastOdometer(data);
        });

        ProjectEvent<MaintenanceRecordDeleted>((data, e) =>
        {
            var item = data.Items.FirstOrDefault(i => i.ItemId == e.ItemId);
            item?.Records.RemoveAll(r => r.RecordId == e.RecordId);

            RecalculateLastOdometer(data);
        });

        // FuelLog events — only affect LastOdometer
        ProjectEvent<FuelLogRecorded>((data, e) =>
        {
            data.VehicleId = e.VehicleId;
            data.UserId = e.UserId;
            RecalculateLastOdometerFromFuelAndMaintenance(data, e.Odometer);
        });

        ProjectEvent<FuelLogUpdated>((data, e) =>
        {
            RecalculateLastOdometerFromFuelAndMaintenance(data, e.Odometer);
        });

        ProjectEvent<FuelLogDeleted>((data, e) =>
        {
            // Cannot determine new last odometer from deletion alone;
            // RecalculateLastOdometer will use maintenance records.
            // FuelLogList.LastOdometer is the source of truth for fuel —
            // we'll read it at query time. See Decision D-07.
        });
    }

    private static void RecalculateLastOdometer(MaintenanceTabData data)
    {
        // From maintenance records only — fuel log odometer handled at query time
        var maxMaintenanceOdometer = data.Items
            .SelectMany(i => i.Records)
            .Select(r => (int?)r.Odometer)
            .Max();

        // LastOdometer will be reconciled with FuelLogList.LastOdometer at query time
        data.LastOdometer = maxMaintenanceOdometer;
    }

    private static void RecalculateLastOdometerFromFuelAndMaintenance(
        MaintenanceTabData data, int fuelOdometer)
    {
        var maxMaintenanceOdometer = data.Items
            .SelectMany(i => i.Records)
            .Select(r => (int?)r.Odometer)
            .Max();

        data.LastOdometer = Math.Max(
            maxMaintenanceOdometer ?? 0,
            fuelOdometer) is 0 ? null : Math.Max(
                maxMaintenanceOdometer ?? 0,
                fuelOdometer);
    }
}
```

**Important: `LastOdometer` reconciliation at query time**

The `MaintenanceTabData.LastOdometer` in the projection tracks the maximum odometer from all sources seen so far. However, on `FuelLogDeleted`, the projection cannot determine the new fuel log max without loading `FuelLogList`. Therefore, the **query endpoint** must reconcile:

```csharp
// At query time in the endpoint:
var tabData = await session.LoadAsync<MaintenanceTabData>(id, ct);
var fuelLogList = await session.LoadAsync<FuelLogList>(FuelLogListId.From(vehicleId), ct);

var lastOdometer = new[] { tabData?.LastOdometer, fuelLogList?.LastOdometer }
    .Where(o => o.HasValue)
    .Select(o => o!.Value)
    .DefaultIfEmpty()
    .Max();
// Use this reconciled lastOdometer for status calculations
```

### MaintenanceTabDataId Helper

```csharp
public static class MaintenanceTabDataId
{
    public static string From(Guid vehicleId) => $"maintenance-{vehicleId}";
}
```

### ProcessedDefineMaintenanceItem (Marten Document — Not a Projection)

Idempotency marker for `DefineMaintenanceItemCommand`.

```csharp
public sealed class ProcessedDefineMaintenanceItem
{
    public string Id { get; set; } = string.Empty;    // "{userId}::{commandId}"
    public string UserId { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public Instant ProcessedAt { get; set; }

    public ProcessedDefineMaintenanceItem() { }

    public ProcessedDefineMaintenanceItem(string id, string userId, Guid itemId, Instant processedAt)
    {
        Id = id;
        UserId = userId;
        ItemId = itemId;
        ProcessedAt = processedAt;
    }
}
```

### ProcessedRecordMaintenance (Marten Document — Not a Projection)

Idempotency marker for `RecordMaintenanceCommand`.

```csharp
public sealed class ProcessedRecordMaintenance
{
    public string Id { get; set; } = string.Empty;    // "{userId}::{commandId}"
    public string UserId { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public Instant ProcessedAt { get; set; }

    public ProcessedRecordMaintenance() { }

    public ProcessedRecordMaintenance(string id, string userId, Guid recordId, Instant processedAt)
    {
        Id = id;
        UserId = userId;
        RecordId = recordId;
        ProcessedAt = processedAt;
    }
}
```

---

## 6. Status Calculation Engine

Status calculation is performed at **query time**, not stored in the projection. This is a pure function that takes the read model data and current context, and produces the status + detail strings for each item.

### MaintenanceStatusCalculator

```csharp
public static class MaintenanceStatusCalculator
{
    public static (string Status, string StatusDetail, LocalDate? LastPerformedDate, int? LastPerformedOdometer)
        Calculate(
            MaintenanceItemEntry item,
            int? estimatedCurrentMileage,
            LocalDate currentDate)
    {
        // Determine effective "last service" baseline
        var (lastDate, lastOdometer) = GetEffectiveBaseline(item);

        // Special case: never performed + no initial state
        if (lastDate is null && lastOdometer is null)
            return (MaintenanceStatus.DueSoon,
                    "No service history — log your first service to start tracking",
                    null, null);

        // Calculate mileage-based status
        var mileageStatus = CalculateMileageStatus(
            item.MileageInterval, item.MileageWarning,
            lastOdometer, estimatedCurrentMileage);

        // Calculate time-based status
        var timeStatus = CalculateTimeStatus(
            item.TimeIntervalMonths, item.TimeWarningDays,
            lastDate, currentDate);

        // Edge case: mileage interval set but no odometer data available
        if (item.MileageInterval.HasValue && estimatedCurrentMileage is null
            && !item.TimeIntervalMonths.HasValue)
        {
            return (MaintenanceStatus.DueSoon,
                    "Log a fill-up or service to start mileage tracking",
                    lastDate, lastOdometer);
        }

        // Combined: worse status wins
        var (finalStatus, finalDetail) = CombineStatuses(
            mileageStatus, timeStatus, item, lastOdometer,
            estimatedCurrentMileage, lastDate, currentDate);

        return (finalStatus, finalDetail, lastDate, lastOdometer);
    }

    private static (LocalDate? Date, int? Odometer) GetEffectiveBaseline(
        MaintenanceItemEntry item)
    {
        // Most recent service record (by date desc, then odometer desc)
        var mostRecent = item.Records
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Odometer)
            .FirstOrDefault();

        if (mostRecent is not null)
            return (mostRecent.Date, mostRecent.Odometer);

        // Fall back to initial baseline
        return (item.InitialLastPerformedDate, item.InitialLastPerformedOdometer);
    }
}
```

### Status Calculation Rules

**Mileage-based** (when `MileageInterval` is set and both `lastOdometer` and `estimatedCurrentMileage` are available):

```
milesSinceService = estimatedCurrentMileage − lastServiceOdometer
if milesSinceService ≥ mileageInterval → Overdue
else if milesSinceService ≥ mileageInterval − mileageWarning → Due Soon
else → OK
```

**Time-based** (when `TimeIntervalMonths` is set and `lastDate` is available):

```
daysSinceService = days between lastServiceDate and currentDate
intervalDays = timeIntervalMonths × 30.44
if daysSinceService ≥ intervalDays → Overdue
else if daysSinceService ≥ intervalDays − timeWarningDays → Due Soon
else → OK
```

**Combined** (both intervals set):
- Calculate both statuses independently
- The **worse** status wins (Overdue > Due Soon > OK)
- Detail string from the dimension that produced the worse status

**Never performed + no initial state**:
- Always returns `DueSoon` with detail: *"No service history — log your first service to start tracking"*
- Never returns `Overdue` for items with no history — prevents alarm fatigue

**Mileage interval set but no odometer data**:
- Returns `DueSoon` with detail: *"Log a fill-up or service to start mileage tracking"*

### Status Detail String Examples

| Scenario | Detail |
|----------|--------|
| OK, mileage only | `"Next due in ~2,300 mi"` |
| OK, time only | `"Next due in 2 months"` |
| OK, both | `"Next due in ~2,300 mi or 2 months"` |
| Due Soon, mileage | `"Due in ~450 mi"` |
| Due Soon, time | `"Due in 1 week"` |
| Due Soon, both (mileage worse) | `"Due in ~450 mi"` |
| Overdue, mileage | `"Overdue by ~800 mi"` |
| Overdue, time | `"Overdue by 3 weeks"` |
| Never performed | `"No service history — log your first service to start tracking"` |
| No odometer data | `"Log a fill-up or service to start mileage tracking"` |

### Estimated Current Mileage (Sprint 04 Scope)

For Sprint 04, `estimatedCurrentMileage` is the **raw most-recent odometer reading from any source** (fuel log or maintenance record) — whichever is higher. Full linear interpolation between the two most recent readings comes in Sprint 05.

The query endpoint computes this as:

```csharp
var estimatedCurrentMileage = new[] { tabData?.LastOdometer, fuelLogList?.LastOdometer }
    .Where(o => o.HasValue)
    .Max();
```

---

## 7. Shared Validation Logic

### MaintenanceItemInput

Validates and normalizes maintenance item definition fields.

```csharp
public static class MaintenanceItemInput
{
    public static string NormalizeAndValidate(
        string name,
        int? mileageInterval,
        int? timeIntervalMonths,
        int mileageWarning,
        int timeWarningDays,
        LocalDate? initialDate,
        int? initialOdometer,
        IClock clock)
    {
        var errors = new Dictionary<string, string[]>();

        // Name validation
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 100)
            errors["name"] = ["Give this item a name"];

        // At least one interval
        if (!mileageInterval.HasValue && !timeIntervalMonths.HasValue)
            errors["intervals"] = ["Set a mileage interval, time interval, or both"];

        // Mileage interval
        if (mileageInterval.HasValue)
        {
            if (mileageInterval.Value is <= 0 or > 999_999)
                errors["mileageInterval"] = ["Enter a valid mileage interval"];
        }

        // Time interval
        if (timeIntervalMonths.HasValue)
        {
            if (timeIntervalMonths.Value is <= 0 or > 120)
                errors["timeIntervalMonths"] = ["Enter a valid time interval (1–120 months)"];
        }

        // Mileage warning threshold
        if (mileageInterval.HasValue && mileageWarning >= mileageInterval.Value)
            errors["mileageWarning"] = ["Warning threshold must be less than the interval"];

        // Time warning threshold
        if (timeIntervalMonths.HasValue)
        {
            var intervalDays = timeIntervalMonths.Value * 30.44;
            if (timeWarningDays >= intervalDays)
                errors["timeWarningDays"] = ["Warning threshold must be less than the interval"];
        }

        // Initial date
        if (initialDate.HasValue)
        {
            var today = clock.GetCurrentInstant()
                .InUtc().Date;
            if (initialDate.Value > today)
                errors["initialLastPerformedDate"] = ["Date can't be in the future"];
        }

        // Initial odometer
        if (initialOdometer.HasValue)
        {
            if (initialOdometer.Value is <= 0 or > 9_999_999)
                errors["initialLastPerformedOdometer"] = ["Enter a valid odometer reading"];
        }

        if (errors.Count > 0)
            throw new MaintenanceValidationException(errors);

        return trimmed;
    }
}
```

### MaintenanceRecordInput

Validates service record fields.

```csharp
public static class MaintenanceRecordInput
{
    public static void Validate(
        LocalDate date,
        int odometer,
        decimal? cost,
        string? notes,
        IClock clock)
    {
        var errors = new Dictionary<string, string[]>();

        // Date: not in the future
        var today = clock.GetCurrentInstant().InUtc().Date;
        if (date > today)
            errors["date"] = ["Date can't be in the future"];

        // Odometer
        if (odometer is <= 0 or > 9_999_999)
            errors["odometer"] = ["Enter a valid odometer reading"];

        // Cost (optional)
        if (cost.HasValue)
        {
            if (cost.Value < 0m || cost.Value > 99_999.99m)
                errors["cost"] = ["Enter a valid cost"];
            else if (decimal.Round(cost.Value, 2) != cost.Value)
                errors["cost"] = ["Enter a valid cost"];
        }

        // Notes (optional)
        if (notes is not null && notes.Length > 500)
            errors["notes"] = ["Notes are too long"];

        if (errors.Count > 0)
            throw new MaintenanceValidationException(errors);
    }
}
```

---

## 8. Exceptions

### MaintenanceValidationException

```csharp
public sealed class MaintenanceValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public MaintenanceValidationException(Dictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
```

### MaintenanceItemNotFoundException

```csharp
public sealed class MaintenanceItemNotFoundException : Exception
{
    public Guid ItemId { get; }

    public MaintenanceItemNotFoundException(Guid itemId)
        : base($"Maintenance item '{itemId}' was not found.")
    {
        ItemId = itemId;
    }
}
```

### MaintenanceRecordNotFoundException

```csharp
public sealed class MaintenanceRecordNotFoundException : Exception
{
    public Guid RecordId { get; }

    public MaintenanceRecordNotFoundException(Guid recordId)
        : base($"Maintenance record '{recordId}' was not found.")
    {
        RecordId = recordId;
    }
}
```

---

## 9. Command Results

### DefineMaintenanceItemResult

```csharp
public sealed record DefineMaintenanceItemResult(Guid ItemId, bool IsDuplicate);
```

### RecordMaintenanceResult

```csharp
public sealed record RecordMaintenanceResult(Guid RecordId, bool IsDuplicate);
```

---

## 10. Marten Configuration

### MaintenanceMartenOptions

```csharp
public static class MaintenanceMartenOptions
{
    public static void Configure(StoreOptions opts)
    {
        // Event types
        opts.Events.AddEventType<MaintenanceItemDefined>();
        opts.Events.AddEventType<MaintenanceItemUpdated>();
        opts.Events.AddEventType<MaintenanceItemRemoved>();
        opts.Events.AddEventType<MaintenancePerformed>();
        opts.Events.AddEventType<MaintenanceRecordUpdated>();
        opts.Events.AddEventType<MaintenanceRecordDeleted>();

        // Projection
        opts.Projections.Add<MaintenanceTabProjection>(ProjectionLifecycle.Inline);

        // Idempotency documents
        opts.Schema.For<ProcessedDefineMaintenanceItem>()
            .Index(x => x.UserId);
        opts.Schema.For<ProcessedRecordMaintenance>()
            .Index(x => x.UserId);

        // Read model index
        opts.Schema.For<MaintenanceTabData>()
            .Index(x => x.UserId);
    }
}
```

---

## 11. File Organization

```
gabMileage.Api/
└── Garage/
    └── Maintenance/
        ├── Commands/
        │   ├── DefineMaintenanceItemCommand.cs
        │   ├── UpdateMaintenanceItemCommand.cs
        │   ├── RemoveMaintenanceItemCommand.cs
        │   ├── RecordMaintenanceCommand.cs
        │   ├── UpdateMaintenanceRecordCommand.cs
        │   └── DeleteMaintenanceRecordCommand.cs
        ├── DomainEvents/
        │   ├── MaintenanceItemDefined.cs
        │   ├── MaintenanceItemUpdated.cs
        │   ├── MaintenanceItemRemoved.cs
        │   ├── MaintenancePerformed.cs
        │   ├── MaintenanceRecordUpdated.cs
        │   └── MaintenanceRecordDeleted.cs
        ├── Handlers/
        │   ├── DefineMaintenanceItemHandler.cs
        │   ├── UpdateMaintenanceItemHandler.cs
        │   ├── RemoveMaintenanceItemHandler.cs
        │   ├── RecordMaintenanceHandler.cs
        │   ├── UpdateMaintenanceRecordHandler.cs
        │   └── DeleteMaintenanceRecordHandler.cs
        ├── Projections/
        │   └── MaintenanceTabProjection.cs
        ├── ReadModels/
        │   ├── MaintenanceTabData.cs
        │   ├── MaintenanceItemEntry.cs
        │   ├── MaintenanceRecordEntry.cs
        │   ├── ProcessedDefineMaintenanceItem.cs
        │   └── ProcessedRecordMaintenance.cs
        ├── ValueObjects/
        │   ├── MileageInterval.cs
        │   ├── TimeInterval.cs
        │   ├── MileageWarningThreshold.cs
        │   ├── TimeWarningThreshold.cs
        │   ├── MaintenanceCost.cs
        │   └── MaintenanceStatus.cs
        ├── Validation/
        │   ├── MaintenanceItemInput.cs
        │   └── MaintenanceRecordInput.cs
        ├── MaintenanceTabDataId.cs
        ├── MaintenanceStatusCalculator.cs
        ├── MaintenanceItemNotFoundException.cs
        ├── MaintenanceRecordNotFoundException.cs
        ├── MaintenanceValidationException.cs
        └── MaintenanceMartenOptions.cs
```

---

## 12. Decisions & Rationale

### D-01: Maintenance events on vehicle stream

- **Who**: domain-modeler
- **What**: All maintenance events (`MaintenanceItemDefined`, `MaintenancePerformed`, etc.) are appended to the `vehicle-{vehicleId}` stream, not to separate per-item or per-maintenance streams.
- **Why**: This is consistent with how FuelLog events are handled in Sprint 03 (Decision D-01). Maintenance items are sub-entities of a vehicle, not independent aggregates. A single vehicle stream keeps the event store simple, avoids stream proliferation, and allows the projection to consume all vehicle-scoped events from one stream.
- **Alternatives considered**: Per-item streams (`maintenance-item-{itemId}`) — rejected because items don't have independent lifecycles from their vehicle, and it would complicate cross-item queries like "all maintenance for vehicle X."
- **Confidence**: High

### D-02: Status computed at query time, not in projection

- **Who**: domain-modeler
- **What**: The `MaintenanceTabProjection` stores raw data (intervals, records, thresholds). The `status` and `statusDetail` fields are computed at query time by `MaintenanceStatusCalculator`, not persisted in the read model.
- **Why**: Status depends on `currentDate` (NodaTime `LocalDate`) and `estimatedCurrentMileage` — both of which change over time without any event being emitted. If status were stored in the projection, it would become stale as time passes (an item could transition from OK → Due Soon → Overdue without any user action). Computing at query time ensures status is always accurate. The calculation is a pure function with negligible cost.
- **Alternatives considered**: (1) Store status in projection and update via a scheduled background job — adds complexity, still has staleness between job runs. (2) Store status and recalculate on every event — still stales due to time passage.
- **Confidence**: High

### D-03: Initial baseline stored in MaintenanceItemDefined event, not as a synthetic record

- **Who**: domain-modeler
- **What**: `InitialLastPerformedDate` and `InitialLastPerformedOdometer` are fields on `MaintenanceItemDefined`, stored as properties of the item in the read model. They are NOT synthetic `MaintenancePerformed` events.
- **Why**: The sprint brief explicitly states "Initial state is stored as a property of the item, not as a service record." Synthetic records would pollute the service history display and create confusion about what's a real service vs. a seed value. The fallback chain is: most recent record → initial baseline → never performed.
- **Alternatives considered**: Emit a synthetic `MaintenancePerformed` event with a flag like `isSeed: true` — rejected because it complicates record deletion (can't delete the seed), clutters history, and the brief explicitly rules it out.
- **Confidence**: High

### D-04: MaintenanceTabData uses separate key prefix

- **Who**: domain-modeler
- **What**: The `MaintenanceTabData` read model uses the document key `"maintenance-{vehicleId}"`, separate from `"vehicle-{vehicleId}"` and `"fuellog-{vehicleId}"`.
- **Why**: Consistent with Sprint 03's `FuelLogList` using `"fuellog-{vehicleId}"`. Each projection produces its own read model document with a distinct key prefix, even though all events live on the same `vehicle-{vehicleId}` stream. This keeps read models independent and avoids key collisions.
- **Alternatives considered**: Reuse the vehicle stream key — rejected because MultiStreamProjection requires its own document key, and mixing read models under the same key would be Marten-incompatible.
- **Confidence**: High

### D-05: Idempotency for DefineMaintenanceItemCommand and RecordMaintenanceCommand

- **Who**: domain-modeler
- **What**: Both creation commands (`DefineMaintenanceItemCommand`, `RecordMaintenanceCommand`) use the `ProcessedCommand` pattern: client provides a `CommandId`, server stores a `Processed{Action}` document keyed by `{userId}::{commandId}`. Update and delete commands do not use idempotency keys (PUT/DELETE are naturally idempotent).
- **Why**: Consistent with Sprint 02 (`ProcessedAddVehicle`) and Sprint 03 (`ProcessedRecordFuelLog`). POST operations need protection against duplicate submissions (network retries, double-clicks). PUT/DELETE operations are naturally idempotent — applying the same update twice produces the same result.
- **Alternatives considered**: Idempotency on all commands — rejected as unnecessary overhead for PUT/DELETE which are already idempotent by HTTP semantics.
- **Confidence**: High

### D-06: UserId denormalized in all events

- **Who**: domain-modeler
- **What**: All six maintenance events include `UserId` (WorkOS user ID string) even though it's technically redundant (derivable from the vehicle ownership).
- **Why**: Consistent with Sprint 03 (FuelLog events include UserId). Enables projection routing without loading the vehicle read model, supports ownership verification in projections, and avoids a lookup when building read models. The cost is a few bytes per event — negligible compared to the operational simplicity.
- **Alternatives considered**: Only include UserId in creation events — rejected because `MaintenanceItemUpdated` needs it for the MultiStreamProjection `Identity<>()` routing, same as `VehicleUpdated` (Sprint 02 Decision D-07).
- **Confidence**: High

### D-07: LastOdometer from both fuel logs and maintenance records

- **Who**: domain-modeler
- **What**: `MaintenanceTabData.LastOdometer` tracks the most recent odometer reading from any source. The projection subscribes to both maintenance events AND fuel log events. The query endpoint reconciles with `FuelLogList.LastOdometer` for accuracy after fuel log deletions.
- **Why**: The sprint brief says "estimatedCurrentMileage: most recent odometer reading from any source (fuel log or maintenance record)." Fuel logs provide frequent odometer snapshots; maintenance records provide odometer at service. The mileage-based status calculation needs the best available estimate. Reconciliation at query time is needed because `FuelLogDeleted` doesn't carry the new max odometer — the projection would need to load `FuelLogList` (cross-document dependency) which is an anti-pattern in projections.
- **Alternatives considered**: (1) Only use fuel log data — rejected because a maintenance record could have a higher odometer than the latest fuel log. (2) Only use maintenance records — rejected because fuel logs are more frequent and provide better mileage estimates. (3) Load FuelLogList in projection — rejected as cross-document dependency in inline projection.
- **Confidence**: Medium — the reconciliation-at-query-time pattern adds a small amount of complexity. If this proves problematic, we could introduce a dedicated `VehicleOdometerTracker` projection that consolidates all odometer sources. However, for Sprint 04's scope (raw most-recent reading, not linear interpolation), the reconciliation approach is sufficient.

### D-08: Duplicate item names allowed

- **Who**: domain-modeler
- **What**: No uniqueness constraint on maintenance item names within a vehicle. A user may have "Tire Rotation — Front" and "Tire Rotation — Rear."
- **Why**: Sprint brief explicitly states: "Yes. A user might have 'Tire Rotation — Front' and 'Tire Rotation — Rear.' No uniqueness constraint on names." Adding a uniqueness check would limit legitimate use cases.
- **Alternatives considered**: Enforce unique names per vehicle — rejected per sprint brief.
- **Confidence**: High

### D-09: Removal is soft-delete via MaintenanceItemRemoved event

- **Who**: domain-modeler
- **What**: Removing a maintenance item appends a `MaintenanceItemRemoved` event. The projection removes the item (and all its records) from the `MaintenanceTabData` read model. The event history is preserved in the event store.
- **Why**: Event sourcing means we never truly delete data — we append a "removed" event. The projection handles the removal by filtering out the item from the read model. This preserves full audit history while giving the user a clean view. Consistent with `FuelLogDeleted` pattern.
- **Alternatives considered**: Hard delete from Marten — impossible in event sourcing. Archive flag instead of removal — adds complexity to every query; the brief says "remove," not "archive."
- **Confidence**: High

### D-10: Time interval in months, warning threshold in days

- **Who**: domain-modeler
- **What**: `TimeIntervalMonths` is expressed in months (int, 1–120). `TimeWarningDays` is expressed in days (int). Conversion uses 30.44 days/month for status calculations.
- **Why**: Sprint brief explicitly specifies: "Months for the interval definition. Days for warning thresholds. Maintenance schedules are universally expressed in months ('every 3 months,' 'every 6 months'). Days would be confusing for intervals. But warning thresholds benefit from day-level granularity ('warn me 14 days before due' is more precise than 'warn me 0.5 months before')."
- **Alternatives considered**: Both in days, both in months — rejected per sprint brief rationale.
- **Confidence**: High

### D-11: Service record sorting is reverse-chronological

- **Who**: domain-modeler
- **What**: Service records within an item are sorted by date descending, then odometer descending. This is maintained in the projection on every record mutation.
- **Why**: The sprint brief specifies "reverse chronological list of records" in the expanded accordion view. Most recent service is most relevant. Consistent with FuelLogList sorting pattern from Sprint 03.
- **Alternatives considered**: Sort at query time — rejected because sorting in the projection avoids repeated sorting on every read and is consistent with Sprint 03's `RecalculateMpgAndSort` pattern.
- **Confidence**: High

### D-12: Items sorted by urgency at query time, not in projection

- **Who**: domain-modeler
- **What**: Maintenance items in the API response are sorted by urgency (overdue → due soon → ok), then alphabetically within each group. This sorting happens at query time, not in the projection.
- **Why**: Status is computed at query time (Decision D-02), so urgency-based sorting must also happen at query time. The projection stores items in insertion order; the query endpoint computes status for each item, then sorts by urgency + name.
- **Alternatives considered**: Store a `sortOrder` field updated by a background process — rejected as overly complex and prone to staleness.
- **Confidence**: High

---

## 13. API Response Shape (Query-Time Enrichment)

The query endpoint loads `MaintenanceTabData` and `FuelLogList`, then enriches each item with computed status:

```csharp
public sealed class MaintenanceTabResponse
{
    public Guid VehicleId { get; set; }
    public IReadOnlyList<MaintenanceItemResponse> Items { get; set; } = [];
    public int? LastOdometer { get; set; }
}

public sealed class MaintenanceItemResponse
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MileageInterval { get; set; }
    public int? TimeIntervalMonths { get; set; }
    public int MileageWarning { get; set; }
    public int TimeWarningDays { get; set; }
    public string Status { get; set; } = string.Empty;          // "ok" | "dueSoon" | "overdue"
    public string StatusDetail { get; set; } = string.Empty;    // Human-readable
    public LocalDate? LastPerformedDate { get; set; }
    public int? LastPerformedOdometer { get; set; }
    public IReadOnlyList<MaintenanceRecordResponse> Records { get; set; } = [];
    public Instant CreatedAt { get; set; }
}

public sealed class MaintenanceRecordResponse
{
    public Guid RecordId { get; set; }
    public Guid ItemId { get; set; }
    public LocalDate Date { get; set; }
    public int Odometer { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public Instant CreatedAt { get; set; }
}
```

---

## Appendix: Cross-Sprint Event Summary

All events on the `vehicle-{vehicleId}` stream after Sprint 04:

| Event | Sprint | Purpose |
|-------|--------|---------|
| `VehicleAdded` | 02 | Vehicle creation |
| `VehicleUpdated` | 02 | Vehicle metadata update |
| `FuelLogRecorded` | 03 | New fill-up |
| `FuelLogUpdated` | 03 | Fill-up edit |
| `FuelLogDeleted` | 03 | Fill-up removal |
| `MaintenanceItemDefined` | 04 | New maintenance item |
| `MaintenanceItemUpdated` | 04 | Item definition edit |
| `MaintenanceItemRemoved` | 04 | Item removal |
| `MaintenancePerformed` | 04 | Service logged |
| `MaintenanceRecordUpdated` | 04 | Service record edit |
| `MaintenanceRecordDeleted` | 04 | Service record removal |
