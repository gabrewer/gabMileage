using NodaTime;

namespace gabMileage.Api.Garage.Vehicles.DomainEvents;

// Breadcrumbs (backend-builder)
// What: Internal domain event emitted when a vehicle stream is created.
// Why: Feeds Marten projections and provides an immutable audit trail.
// Alternatives considered: Storing only a snapshot document (rejected: event sourcing is default).
// Confidence: high
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
