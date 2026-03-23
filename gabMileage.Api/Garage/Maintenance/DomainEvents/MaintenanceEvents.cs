using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.DomainEvents;

// Internal domain events for Maintenance. These are persisted in Marten event streams and drive projections.

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
    Instant OccurredAt);

public sealed record MaintenanceItemUpdated(
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    string Name,
    int? MileageInterval,
    int? TimeIntervalMonths,
    int MileageWarning,
    int TimeWarningDays,
    Instant OccurredAt);

public sealed record MaintenanceItemRemoved(
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    Instant OccurredAt);

public sealed record MaintenancePerformed(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    LocalDate Date,
    int Odometer,
    string? Notes,
    Instant OccurredAt);

public sealed record MaintenanceRecordUpdated(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    LocalDate Date,
    int Odometer,
    string? Notes,
    Instant OccurredAt);

public sealed record MaintenanceRecordDeleted(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId,
    string UserId,
    Guid CommandId,
    Instant OccurredAt);
