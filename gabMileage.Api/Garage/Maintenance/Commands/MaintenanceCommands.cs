using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.Commands;

// Command contracts for Maintenance vertical slice (TASK-001).
// Commands carry normalized values (defaults applied at HTTP boundary / handler normalization).

public sealed record DefineMaintenanceItemCommand(
    Guid CommandId,
    Guid VehicleId,
    string Name,
    int? MileageInterval,
    int? TimeIntervalMonths,
    int MileageWarning,
    int TimeWarningDays,
    LocalDate? InitialLastPerformedDate,
    int? InitialLastPerformedOdometer);

public sealed record UpdateMaintenanceItemCommand(
    Guid CommandId,
    Guid VehicleId,
    Guid ItemId,
    string Name,
    int? MileageInterval,
    int? TimeIntervalMonths,
    int MileageWarning,
    int TimeWarningDays);

public sealed record RemoveMaintenanceItemCommand(
    Guid CommandId,
    Guid VehicleId,
    Guid ItemId);

public sealed record RecordMaintenanceCommand(
    Guid CommandId,
    Guid VehicleId,
    Guid ItemId,
    LocalDate Date,
    int Odometer,
    string? Notes);

public sealed record UpdateMaintenanceRecordCommand(
    Guid CommandId,
    Guid VehicleId,
    Guid ItemId,
    Guid RecordId,
    LocalDate Date,
    int Odometer,
    string? Notes);

public sealed record DeleteMaintenanceRecordCommand(
    Guid RecordId,
    Guid ItemId,
    Guid VehicleId);
