using NodaTime;

namespace gabMileage.Api.Garage.Vehicles.ReadModels;

// Breadcrumbs (backend-builder)
// What: Idempotency marker document for AddVehicleCommand.
// Why: Ensures commandId duplicates are no-ops (server-side idempotency) and provides a commandId -> vehicleId mapping.
// Alternatives considered: Relying on stream uniqueness only (rejected: commandId is the required idempotency key).
// Confidence: high
public sealed record ProcessedAddVehicle(
    string Id,               // Composite key: {userId}:{commandId}
    string UserId,
    Guid VehicleId,
    string VehicleStreamId,
    Instant ProcessedAt
);
