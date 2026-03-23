using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Maintenance.Commands;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.Handlers;

public static class DeleteMaintenanceRecordHandler
{
    public static async Task Handle(
        DeleteMaintenanceRecordCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth.UserId))
            throw new InvalidOperationException("Authenticated user is required.");

        if (command.VehicleId == Guid.Empty)
            throw new ArgumentException("VehicleId is required.", nameof(command.VehicleId));

        if (command.ItemId == Guid.Empty)
            throw new ArgumentException("ItemId is required.", nameof(command.ItemId));

        if (command.RecordId == Guid.Empty)
            throw new ArgumentException("RecordId is required.", nameof(command.RecordId));

        // Ownership check (vehicle)
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        var maintenanceTab = await session.LoadAsync<MaintenanceTabData>(MaintenanceTabDataId.From(command.VehicleId), cancellationToken);

        var item = maintenanceTab?.Items.FirstOrDefault(i => i.ItemId == command.ItemId);
        if (item is null)
            throw new MaintenanceItemNotFoundException(command.ItemId);

        var record = item.Records.FirstOrDefault(r => r.RecordId == command.RecordId);
        if (record is null)
            throw new MaintenanceRecordNotFoundException(command.RecordId);

        var now = clock.GetCurrentInstant();

        // Breadcrumbs (backend-builder)
        // Who: backend-builder
        // What: DeleteMaintenanceRecordCommand carries only route IDs; handler generates a new CommandId for the persisted event.
        // Why: Sprint 04 domain model treats DELETE as naturally idempotent without a processed-command marker, but events still
        //      include a CommandId for traceability.
        // Alternatives considered: require client-supplied CommandId (rejected: violates TASK-006 contract).
        // Confidence: high
        var commandId = Guid.NewGuid();

        session.Events.Append(vehicleStreamId, new MaintenanceRecordDeleted(
            command.RecordId,
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            commandId,
            now));

        await session.SaveChangesAsync(cancellationToken);
    }
}
