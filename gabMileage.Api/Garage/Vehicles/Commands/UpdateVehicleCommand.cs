namespace gabMileage.Api.Garage.Vehicles.Commands;

// Breadcrumbs (backend-builder)
// What: Define UpdateVehicleCommand write-model command for updating vehicle details.
// Why: Updates must append VehicleUpdated events (event sourcing) rather than replacing a document.
// Alternatives considered: Direct document update of VehicleReadModel (rejected: loses event history).
// Confidence: high
public sealed record UpdateVehicleCommand(
    Guid VehicleId,
    string Name,
    string? Make,
    string? Model,
    int? Year
);
