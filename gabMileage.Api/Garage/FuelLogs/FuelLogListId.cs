namespace gabMileage.Api.Garage.FuelLogs;

// Breadcrumbs (backend-builder)
// Who: backend-builder
// What: Centralize string document id format for FuelLogList read model.
// Why: Domain spec keys FuelLogList by a distinct prefix ("fuellog-") to avoid collisions with VehicleReadModel ids.
// Alternatives considered: Use vehicle stream id directly (rejected: ambiguous, collides with other per-vehicle read models).
// Confidence: high
public static class FuelLogListId
{
    public static string From(Guid vehicleId) => $"fuellog-{vehicleId:D}";
}
