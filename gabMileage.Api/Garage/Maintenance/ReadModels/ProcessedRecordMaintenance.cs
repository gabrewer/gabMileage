using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.ReadModels;

// Breadcrumbs (backend-builder)
// What: Idempotency marker document for RecordMaintenanceCommand.
// Why: Duplicate commandId submissions must be safe no-ops and return the previously created RecordId.
// Alternatives considered: Stream-only uniqueness (rejected: idempotency key is commandId, not recordId).
// Confidence: high
public sealed class ProcessedRecordMaintenance
{
    public string Id { get; set; } = string.Empty;    // "{userId}::{commandId}"
    public string UserId { get; set; } = string.Empty;

    // Used to detect accidental commandId reuse against a different vehicle/item.
    public Guid VehicleId { get; set; }
    public Guid ItemId { get; set; }

    public Guid RecordId { get; set; }
    public Instant ProcessedAt { get; set; }

    public ProcessedRecordMaintenance() { }

    public ProcessedRecordMaintenance(string id, string userId, Guid vehicleId, Guid itemId, Guid recordId, Instant processedAt)
    {
        Id = id;
        UserId = userId;
        VehicleId = vehicleId;
        ItemId = itemId;
        RecordId = recordId;
        ProcessedAt = processedAt;
    }
}
