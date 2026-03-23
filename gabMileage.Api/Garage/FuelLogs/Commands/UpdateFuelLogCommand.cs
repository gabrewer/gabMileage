using NodaTime;

namespace gabMileage.Api.Garage.FuelLogs.Commands;

public sealed record UpdateFuelLogCommand(
    Guid FuelLogId,
    Guid VehicleId,
    LocalDate Date,
    int Odometer,
    decimal? Gallons,
    decimal? TotalCost,
    decimal? PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes
);
