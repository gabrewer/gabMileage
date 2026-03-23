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
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["commandId"] = ["CommandId is required."] });

        if (command.VehicleId == Guid.Empty)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

        if (command.ItemId == Guid.Empty)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["itemId"] = ["ItemId is required."] });

        if (command.RecordId == Guid.Empty)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["recordId"] = ["RecordId is required."] });

        // Guardrail (TASK-001 destroyer test): reject invalid odometer before any DB work.
        if (command.Odometer <= 0)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["odometer"] = ["Enter a valid odometer reading"] });

        MaintenanceRecordInput.Validate(command.Date, command.Odometer, command.Notes, clock);

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
            throw new MaintenanceRecordNotFoundException(command.RecordId);

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
