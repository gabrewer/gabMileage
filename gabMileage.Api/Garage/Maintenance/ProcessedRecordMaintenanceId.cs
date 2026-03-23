namespace gabMileage.Api.Garage.Maintenance;

public static class ProcessedRecordMaintenanceId
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Composite key generator for ProcessedRecordMaintenance idempotency marker.
    // Why: TASK-001 requires idempotency scoped to (userId, commandId).
    // Alternatives considered: Use recordId as idempotency key (rejected: client supplies commandId).
    // Confidence: high
    public static string From(string userId, Guid commandId) => $"{userId}::{commandId:D}";
}
