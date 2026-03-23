namespace gabMileage.Api.Garage.Vehicles;

public static class ProcessedAddVehicleId
{
    public static string From(string userId, Guid commandId) => $"{userId}:{commandId:D}";
}
