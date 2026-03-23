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
            throw new ArgumentException("CommandId is required.", nameof(command.CommandId));

        if (command.VehicleId == Guid.Empty)
            throw new ArgumentException("VehicleId is required.", nameof(command.VehicleId));

        if (command.ItemId == Guid.Empty)
            throw new ArgumentException("ItemId is required.", nameof(command.ItemId));

        // Invariant: a maintenance item must have at least one positive interval (miles and/or months).
        if (command.MileageInterval is not null && command.MileageInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(command.MileageInterval), "MileageInterval must be positive when provided.");

        if (command.TimeIntervalMonths is not null && command.TimeIntervalMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(command.TimeIntervalMonths), "TimeIntervalMonths must be positive when provided.");

        if (command.MileageInterval is null && command.TimeIntervalMonths is null)
            throw new ArgumentException("At least one interval (MileageInterval or TimeIntervalMonths) is required.", nameof(command));

        // Ownership check (vehicle)
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        var maintenanceTab = await session.LoadAsync<MaintenanceTabData>(MaintenanceTabDataId.From(command.VehicleId), cancellationToken);
        var itemExists = maintenanceTab?.Items.Any(i => i.ItemId == command.ItemId) ?? false;
        if (!itemExists)
            throw new InvalidOperationException($"Maintenance item '{command.ItemId}' not found on vehicle '{command.VehicleId}'.");

        var (name, mileageWarning, timeWarningDays) = MaintenanceItemInput.NormalizeAndValidate(
            command.Name,
            command.MileageWarning,
            command.TimeWarningDays);

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
