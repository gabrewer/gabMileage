using gabMileage.Api.Auth;
using gabMileage.Api.Garage.FuelLogs.Commands;
using gabMileage.Api.Garage.FuelLogs.DomainEvents;
using gabMileage.Api.Garage.FuelLogs.ReadModels;
using gabMileage.Api.Garage.FuelLogs.Validation;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;

namespace gabMileage.Api.Garage.FuelLogs.Handlers;

public static class UpdateFuelLogHandler
{
    public static async Task Handle(
        UpdateFuelLogCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth.UserId))
            throw new InvalidOperationException("Authenticated user is required.");

        if (command.VehicleId == Guid.Empty)
            throw new FuelLogValidationException(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

        if (command.FuelLogId == Guid.Empty)
            throw new FuelLogValidationException(new Dictionary<string, string[]> { ["fuelLogId"] = ["FuelLogId is required."] });

        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        var fuelLogList = await session.LoadAsync<FuelLogList>(FuelLogListId.From(command.VehicleId), cancellationToken);
        if (fuelLogList is null || !fuelLogList.Logs.Any(l => l.FuelLogId == command.FuelLogId))
            throw new FuelLogNotFoundException(command.FuelLogId);

        FuelLogInput.Validate(command.Date, command.Odometer, command.Notes, clock);

        var (gallons, totalCost, pricePerGallon, calculatedField) = FuelFieldCalculator.NormalizeAndValidate(
            command.Gallons,
            command.TotalCost,
            command.PricePerGallon,
            command.CalculatedField);

        var now = clock.GetCurrentInstant();

        session.Events.Append(vehicleStreamId, new FuelLogUpdated(
            command.FuelLogId,
            command.VehicleId,
            auth.UserId,
            command.Date,
            command.Odometer,
            gallons,
            totalCost,
            pricePerGallon,
            calculatedField,
            command.IsPartialFillUp,
            string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            now));

        await session.SaveChangesAsync(cancellationToken);
    }
}
