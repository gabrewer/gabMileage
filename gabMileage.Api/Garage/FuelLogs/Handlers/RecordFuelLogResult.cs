namespace gabMileage.Api.Garage.FuelLogs.Handlers;

public sealed record RecordFuelLogResult(Guid FuelLogId, bool IsDuplicate, string? OdometerWarning);
