using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Maintenance.Commands;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using gabMileage.Api.Garage.Maintenance.Validation;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;
using Npgsql;

namespace gabMileage.Api.Garage.Maintenance.Handlers;

public sealed record DefineMaintenanceItemResult(Guid ItemId, bool IsDuplicate);

public static class DefineMaintenanceItemHandler
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Define a maintenance item by appending MaintenanceItemDefined to the vehicle-{vehicleId} stream.
    // Why: Sprint 04 adds HTTP idempotency + brief validation messages while keeping all maintenance events on the vehicle stream.
    // Alternatives considered: Separate maintenanceitem-{itemId} streams (rejected: projection and spec center around vehicle maintenance tab).
    // Confidence: medium
    public static async Task<DefineMaintenanceItemResult> Handle(
        DefineMaintenanceItemCommand command,
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

        // Invariant: a maintenance item must have at least one positive interval (miles and/or months).
        // (Keep explicit branching here as a guardrail signal; user-facing messages are Sprint 04 brief strings.)
        if (command.MileageInterval is not null && command.MileageInterval <= 0)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["mileageInterval"] = ["Enter a valid mileage interval"] });

        if (command.TimeIntervalMonths is not null && command.TimeIntervalMonths <= 0)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["timeIntervalMonths"] = ["Enter a valid time interval (1–120 months)"] });

        if (command.MileageInterval is null && command.TimeIntervalMonths is null)
            throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["intervals"] = ["Set a mileage interval, time interval, or both"] });

        var markerId = ProcessedDefineMaintenanceItemId.From(auth.UserId, command.CommandId);

        // Ownership check (vehicle)
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        // Idempotency check (after ownership validation to preserve 404 semantics)
        var processed = await session.LoadAsync<ProcessedDefineMaintenanceItem>(markerId, cancellationToken);
        if (processed is not null)
        {
            if (!string.Equals(processed.UserId, auth.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency marker user mismatch.");

            // Back-compat: older markers may not have VehicleId populated.
            if (processed.VehicleId != Guid.Empty && processed.VehicleId != command.VehicleId)
                throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["commandId"] = ["CommandId has already been used for a different maintenance item."] });

            return new DefineMaintenanceItemResult(processed.ItemId, IsDuplicate: true);
        }

        var (name, mileageWarning, timeWarningDays) = MaintenanceItemInput.NormalizeAndValidate(
            command.Name,
            command.MileageInterval,
            command.TimeIntervalMonths,
            command.MileageWarning,
            command.TimeWarningDays,
            command.InitialLastPerformedDate,
            command.InitialLastPerformedOdometer,
            clock);

        // Acceptance criteria #1: mileageWarning defaults to 500 and timeWarningDays defaults to 14 when omitted.
        // With System.Text.Json, omitted non-nullable ints deserialize as 0.
        if (command.MileageWarning is 0) mileageWarning = MileageWarningThreshold.Default; // 500
        if (command.TimeWarningDays is 0) timeWarningDays = TimeWarningThreshold.DefaultDays; // 14

        // Explicit 0/negative -> default guardrails.
        if (mileageWarning <= 0) mileageWarning = MileageWarningThreshold.Default;
        if (timeWarningDays <= 0) timeWarningDays = TimeWarningThreshold.DefaultDays;

        var itemId = Guid.NewGuid();
        var now = clock.GetCurrentInstant();

        var defined = new MaintenanceItemDefined(
            itemId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            name,
            command.MileageInterval,
            command.TimeIntervalMonths,
            mileageWarning,
            timeWarningDays,
            command.InitialLastPerformedDate,
            command.InitialLastPerformedOdometer,
            now);

        session.Events.Append(vehicleStreamId, defined);

        // Persist the idempotency marker in the same transaction as the event append.
        session.Insert(new ProcessedDefineMaintenanceItem(markerId, auth.UserId, command.VehicleId, itemId, now));

        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return new DefineMaintenanceItemResult(itemId, IsDuplicate: false);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            var existing = await session.LoadAsync<ProcessedDefineMaintenanceItem>(markerId, cancellationToken);
            if (existing is null)
                throw;

            if (!string.Equals(existing.UserId, auth.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency marker user mismatch.");

            if (existing.VehicleId != Guid.Empty && existing.VehicleId != command.VehicleId)
                throw new MaintenanceValidationException(new Dictionary<string, string[]> { ["commandId"] = ["CommandId has already been used for a different maintenance item."] });

            return new DefineMaintenanceItemResult(existing.ItemId, IsDuplicate: true);
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
