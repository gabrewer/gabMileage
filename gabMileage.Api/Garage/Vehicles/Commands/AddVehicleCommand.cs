namespace gabMileage.Api.Garage.Vehicles.Commands;

// Breadcrumbs (backend-builder)
// What: Define AddVehicleCommand write-model command for creating a new vehicle.
// Why: Commands are the write-side contract for Wolverine handlers and Marten event streams.
// Alternatives considered: Ad-hoc endpoint DTO only (rejected: tests + domain docs require explicit command type).
// Confidence: high
public sealed record AddVehicleCommand(
    Guid CommandId,
    string Name,
    string? Make,
    string? Model,
    int? Year
);
