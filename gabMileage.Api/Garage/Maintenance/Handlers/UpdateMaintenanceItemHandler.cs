using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Maintenance.Commands;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using gabMileage.Api.Garage.Maintenance.Validation;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.Handlers;

public static class UpdateMaintenanceItemHandler
{
    public static async Task Handle(
        UpdateMaintenanceItemCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth.UserId))
            throw new InvalidOperationException("Authenticated user is required.");

        if (command.CommandId == Guid.Empty)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["commandId"] = ["CommandId is required."] });

        if (command.VehicleId == Guid.Empty)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

        if (command.ItemId == Guid.Empty)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["itemId"] = ["ItemId is required."] });

        // Invariant: a maintenance item must have at least one positive interval (miles and/or months).
        // (The more user-friendly messages are produced by MaintenanceItemInput.)
        if (command.MileageInterval is not null && command.MileageInterval <= 0)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["mileageInterval"] = ["Enter a valid mileage interval"] });

        if (command.TimeIntervalMonths is not null && command.TimeIntervalMonths <= 0)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["timeIntervalMonths"] = ["Enter a valid time interval (1–120 months)"] });

        if (command.MileageInterval is null && command.TimeIntervalMonths is null)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["intervals"] = ["Set a mileage interval, time interval, or both"] });

        // Ownership check (vehicle)
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        var maintenanceTab = await session.LoadAsync<MaintenanceTabData>(MaintenanceTabDataId.From(command.VehicleId), cancellationToken);
        var itemExists = maintenanceTab?.Items.Any(i => i.ItemId == command.ItemId) ?? false;
        if (!itemExists)
            throw new MaintenanceItemNotFoundException(command.ItemId);

        var (name, mileageWarning, timeWarningDays) = MaintenanceItemInput.NormalizeAndValidate(
            command.Name,
            command.MileageInterval,
            command.TimeIntervalMonths,
            command.MileageWarning,
            command.TimeWarningDays,
            initialLastPerformedDate: null,
            initialLastPerformedOdometer: null,
            clock);

        var now = clock.GetCurrentInstant();

        session.Events.Append(vehicleStreamId, new MaintenanceItemUpdated(
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            name,
            command.MileageInterval,
            command.TimeIntervalMonths,
            mileageWarning,
            timeWarningDays,
            now));

        await session.SaveChangesAsync(cancellationToken);
    }
}
