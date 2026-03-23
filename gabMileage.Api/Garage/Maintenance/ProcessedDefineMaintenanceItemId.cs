namespace gabMileage.Api.Garage.Maintenance;

public static class ProcessedDefineMaintenanceItemId
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Composite key generator for define-item idempotency marker.
    // Why: Sprint 04 POST /maintenance/items is idempotent via (userId, commandId).
    // Alternatives considered: Scope to vehicleId as well (rejected: matches existing fuel-log and record-maintenance idempotency style).
    // Confidence: high
    public static string From(string userId, Guid commandId) => $"{userId}::{commandId:D}";
}
