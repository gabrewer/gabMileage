namespace gabMileage.Api.Garage.FuelLogs;

public sealed class FuelLogNotFoundException : Exception
{
    public FuelLogNotFoundException(Guid fuelLogId)
        : base($"FuelLog '{fuelLogId}' was not found")
    {
        FuelLogId = fuelLogId;
    }

    public Guid FuelLogId { get; }
}
