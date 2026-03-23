namespace gabMileage.Api.Garage.FuelLogs.ReadModels;

public sealed class FuelLogList
{
    public string Id { get; set; } = string.Empty;            // "fuellog-{vehicleId}"
    public Guid VehicleId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<FuelLogEntry> Logs { get; set; } = [];
    public int? LastOdometer { get; set; }
}
