using NodaTime;

namespace gabMileage.Api.Garage.FuelLogs.DomainEvents;

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
