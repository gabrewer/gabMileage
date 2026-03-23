using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Maintenance.Commands;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.Handlers;

public static class UpdateMaintenanceRecordHandler
{
    public static async Task Handle(
        UpdateMaintenanceRecordCommand command,
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

        if (command.RecordId == Guid.Empty)
            throw new ArgumentException("RecordId is required.", nameof(command.RecordId));

        if (command.Odometer <= 0)
            throw new ArgumentOutOfRangeException(nameof(command.Odometer), "Odometer must be positive.");

        // Ownership check (vehicle)
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        var maintenanceTab = await session.LoadAsync<MaintenanceTabData>(MaintenanceTabDataId.From(command.VehicleId), cancellationToken);
        var recordExists = maintenanceTab?.Items
            .FirstOrDefault(i => i.ItemId == command.ItemId)
            ?.Records.Any(r => r.RecordId == command.RecordId) ?? false;

        if (!recordExists)
            throw new InvalidOperationException($"Maintenance record '{command.RecordId}' not found.");

        var now = clock.GetCurrentInstant();

        session.Events.Append(vehicleStreamId, new MaintenanceRecordUpdated(
            command.RecordId,
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            command.Date,
            command.Odometer,
            string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            now));

        await session.SaveChangesAsync(cancellationToken);
    }
}
