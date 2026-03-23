using NodaTime;

namespace gabMileage.Api.Garage.ReadModels;

// Breadcrumbs (backend-builder)
// Who: backend-builder
// What: Define the GarageSummary read model contract for Sprint 02 (vehicles list + card fields).
// Why: The frontend needs a projection-backed summary that can be loaded in a single query and rendered as cards.
// Alternatives considered: Returning VehicleReadModel list directly (rejected: summary contract is a stable API shape).
// Confidence: high
public sealed class GarageSummary
{
    // This is also used as the Marten document identity (configured via StoreOptions.Schema.For<GarageSummary>().Identity(x => x.UserId)).
    public string UserId { get; set; } = string.Empty;

    public IReadOnlyList<GarageSummaryVehicle> Vehicles { get; set; }
        = Array.Empty<GarageSummaryVehicle>();
}

public sealed class GarageSummaryVehicle
{
    public Guid VehicleId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }

    // Sprint 02 placeholders — photo infrastructure comes in a later sprint.
    public string? PhotoUrl { get; set; }
    public string? PhotoThumbnailUrl { get; set; }

    public Instant CreatedAt { get; set; }
}
