using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Maintenance.Commands;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;
using Npgsql;

namespace gabMileage.Api.Garage.Maintenance.Handlers;

public sealed record RecordMaintenanceResult(Guid RecordId, bool IsDuplicate);

public static class RecordMaintenanceHandler
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Handle RecordMaintenanceCommand by appending MaintenancePerformed to the vehicle-{vehicleId} stream.
    // Why: TASK-001 acceptance criteria #2 requires a service record + idempotency via commandId.
    // Alternatives considered: Separate maintenancerecord-{recordId} streams (rejected: spec projects records into vehicle maintenance tab).
    // Confidence: high
    public static async Task<RecordMaintenanceResult> Handle(
        RecordMaintenanceCommand command,
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

        if (command.Odometer <= 0)
            throw new ArgumentOutOfRangeException(nameof(command.Odometer), "Odometer must be positive.");

        var markerId = ProcessedRecordMaintenanceId.From(auth.UserId, command.CommandId);

        // Idempotency check
        var processed = await session.LoadAsync<ProcessedRecordMaintenance>(markerId, cancellationToken);
        if (processed is not null)
        {
            if (!string.Equals(processed.UserId, auth.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency marker user mismatch.");

            return new RecordMaintenanceResult(processed.RecordId, IsDuplicate: true);
        }

        // Ownership check (vehicle)
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        // Verify item exists via current projection (inline for read-after-write).
        var maintenanceTab = await session.LoadAsync<MaintenanceTabData>(MaintenanceTabDataId.From(command.VehicleId), cancellationToken);
        var itemExists = maintenanceTab?.Items.Any(i => i.ItemId == command.ItemId) ?? false;
        if (!itemExists)
            throw new InvalidOperationException($"Maintenance item '{command.ItemId}' not found on vehicle '{command.VehicleId}'.");

        var recordId = Guid.NewGuid();
        var now = clock.GetCurrentInstant();

        var performed = new MaintenancePerformed(
            recordId,
            command.ItemId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            command.Date,
            command.Odometer,
            string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            now);

        session.Events.Append(vehicleStreamId, performed);

        // Persist the idempotency marker in the same transaction as the event append.
        session.Insert(new ProcessedRecordMaintenance(markerId, auth.UserId, recordId, now));

        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return new RecordMaintenanceResult(recordId, IsDuplicate: false);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            var existing = await session.LoadAsync<ProcessedRecordMaintenance>(markerId, cancellationToken);
            if (existing is null)
                throw;

            return new RecordMaintenanceResult(existing.RecordId, IsDuplicate: true);
        }
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;
        }

        return false;
    }
}
