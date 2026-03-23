namespace gabMileage.Api.Garage.FuelLogs;

public static class ProcessedRecordFuelLogId
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Composite key generator for ProcessedRecordFuelLog idempotency marker.
    // Why: Sprint 03 specifies idempotency scoped to (userId, commandId).
    // Alternatives considered: Reuse ProcessedAddVehicleId format (rejected: spec uses "::{commandId}" and we want clear separation).
    // Confidence: high
    public static string From(string userId, Guid commandId) => $"{userId}::{commandId:D}";
}
