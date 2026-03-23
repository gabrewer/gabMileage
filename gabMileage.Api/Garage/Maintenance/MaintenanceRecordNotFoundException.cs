namespace gabMileage.Api.Garage.Maintenance;

public sealed class MaintenanceRecordNotFoundException : Exception
{
    public MaintenanceRecordNotFoundException(Guid recordId)
        : base($"Maintenance record {recordId:D} not found.")
    {
        RecordId = recordId;
    }

    public Guid RecordId { get; }
}
