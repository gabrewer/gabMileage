namespace gabMileage.Api.Garage.Vehicles.Handlers;

public sealed record AddVehicleResult(Guid VehicleId, bool IsDuplicate);
