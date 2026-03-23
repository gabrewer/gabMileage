using NodaTime;

namespace gabMileage.Api.Garage.FuelLogs.ReadModels;

// Breadcrumbs (backend-builder)
// What: Idempotency marker document for RecordFuelLogCommand.
// Why: Ensures duplicate commandId submissions are safe no-ops and returns the previously created FuelLogId.
// Alternatives considered: Stream-only uniqueness (rejected: idempotency key is commandId, not fuelLogId).
// Confidence: high
public sealed class ProcessedRecordFuelLog
{
    public string Id { get; set; } = string.Empty;    // "{userId}::{commandId}"
    public string UserId { get; set; } = string.Empty;
    public Guid FuelLogId { get; set; }
    public Instant ProcessedAt { get; set; }

    public ProcessedRecordFuelLog() { }

    public ProcessedRecordFuelLog(string id, string userId, Guid fuelLogId, Instant processedAt)
    {
        Id = id;
        UserId = userId;
        FuelLogId = fuelLogId;
        ProcessedAt = processedAt;
    }
}
