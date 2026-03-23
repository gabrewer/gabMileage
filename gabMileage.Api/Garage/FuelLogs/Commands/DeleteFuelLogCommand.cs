namespace gabMileage.Api.Garage.FuelLogs.Commands;

public sealed record DeleteFuelLogCommand(
    Guid FuelLogId,
    Guid VehicleId
);
