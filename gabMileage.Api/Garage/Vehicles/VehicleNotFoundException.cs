namespace gabMileage.Api.Garage.Vehicles;

public sealed class VehicleNotFoundException : Exception
{
    public VehicleNotFoundException(Guid vehicleId)
        : base($"Vehicle '{vehicleId}' was not found")
    {
        VehicleId = vehicleId;
    }

    public Guid VehicleId { get; }
}
