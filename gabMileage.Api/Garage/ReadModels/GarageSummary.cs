namespace gabMileage.Api.Garage.ReadModels;

// Breadcrumbs (backend-builder)
// What: Define GarageSummary read model contract in the Api (Sprint 01: empty vehicles list).
// Why: Establishes a stable JSON shape for the frontend before Vehicle events/projections exist.
// Alternatives considered: No endpoint/model until vehicles exist (rejected: frontend needs empty-state contract).
// Confidence: high
public sealed class GarageSummary
{
    public string UserId { get; set; } = string.Empty;

    public IReadOnlyList<GarageSummaryVehicle> Vehicles { get; set; }
        = Array.Empty<GarageSummaryVehicle>();
}

// Sprint 01 placeholder; Sprint 02 will extend this type with actual fields.
public sealed class GarageSummaryVehicle
{
}
