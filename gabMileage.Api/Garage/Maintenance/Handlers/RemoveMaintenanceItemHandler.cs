using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Maintenance.Commands;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.Handlers;

public static class RemoveMaintenanceItemHandler
{
    public static async Task Handle(
        RemoveMaintenanceItemCommand command,
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

        // Ownership check (vehicle)
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        var maintenanceTab = await session.LoadAsync<MaintenanceTabData>(MaintenanceTabDataId.From(command.VehicleId), cancellationToken);
        var itemExists = maintenanceTab?.Items.Any(i => i.ItemId == command.ItemId) ?? false;
        if (!itemExists)
            return; // remove is idempotent

        var now = clock.GetCurrentInstant();

        session.Events.Append(vehicleStreamId, new MaintenanceItemRemoved(
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            now));

        await session.SaveChangesAsync(cancellationToken);
    }
}
