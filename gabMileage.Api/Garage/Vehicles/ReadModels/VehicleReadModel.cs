using NodaTime;

namespace gabMileage.Api.Garage.Vehicles.ReadModels;

// Breadcrumbs (backend-builder)
// What: Vehicle read model (projection target) for query-side consumption.
// Why: Keeps query-side stable and fast while write-side remains event sourced.
// Alternatives considered: Querying raw events for reads (rejected: inefficient and complicates API contracts).
// Confidence: high
public sealed class VehicleReadModel
{
    // Marten stream identity is string (StreamIdentity.AsString), so we use a string document identity.
    // This allows us to keep VehicleId as a Guid field while projecting by stream id.
    public string StreamId { get; set; } = string.Empty;

    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }

    public string? PhotoUrl { get; set; }
    public string? PhotoThumbnailUrl { get; set; }

    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
}
