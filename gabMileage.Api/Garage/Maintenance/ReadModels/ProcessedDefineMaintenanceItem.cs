using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.ReadModels;

// Breadcrumbs (backend-builder)
// What: Idempotency marker document for DefineMaintenanceItemCommand.
// Why: Duplicate commandId submissions must be safe no-ops and return the previously created ItemId.
// Alternatives considered: Stream-only uniqueness (rejected: idempotency key is commandId).
// Confidence: high
public sealed class ProcessedDefineMaintenanceItem
{
    public string Id { get; set; } = string.Empty;    // "{userId}::{commandId}"
    public string UserId { get; set; } = string.Empty;

    // Used to detect accidental commandId reuse against a different vehicle.
    public Guid VehicleId { get; set; }

    public Guid ItemId { get; set; }
    public Instant ProcessedAt { get; set; }

    public ProcessedDefineMaintenanceItem() { }

    public ProcessedDefineMaintenanceItem(string id, string userId, Guid vehicleId, Guid itemId, Instant processedAt)
    {
        Id = id;
        UserId = userId;
        VehicleId = vehicleId;
        ItemId = itemId;
        ProcessedAt = processedAt;
    }
}
