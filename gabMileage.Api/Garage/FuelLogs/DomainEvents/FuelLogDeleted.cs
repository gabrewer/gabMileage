using NodaTime;

namespace gabMileage.Api.Garage.FuelLogs.DomainEvents;

public sealed record FuelLogDeleted(
    Guid FuelLogId,
    Guid VehicleId,
    string UserId,
    Instant OccurredAt
);
