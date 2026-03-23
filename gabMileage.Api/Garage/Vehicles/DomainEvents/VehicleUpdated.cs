using NodaTime;

namespace gabMileage.Api.Garage.Vehicles.DomainEvents;

// Breadcrumbs (backend-builder)
// What: Internal domain event emitted when vehicle details change.
// Why: VehicleUpdated includes UserId (denormalized) for future GarageSummary projection routing.
// Alternatives considered: Derive UserId from stream metadata (rejected: routing needs explicit field per docs).
// Confidence: high
public sealed record VehicleUpdated(
    Guid VehicleId,
    string UserId,
    string Name,
    string? Make,
    string? Model,
    int? Year,
    Instant OccurredAt
);
