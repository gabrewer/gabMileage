using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Maintenance.Commands;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.Validation;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.Handlers;

public static class DefineMaintenanceItemHandler
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Define a maintenance item by appending MaintenanceItemDefined to the vehicle-{vehicleId} stream.
    // Why: TASK-001 keeps maintenance events on the vehicle stream and requires 0->default warning threshold behavior.
    // Alternatives considered: Separate maintenanceitem-{itemId} streams (rejected: projection and spec center around vehicle maintenance tab).
    // Confidence: medium
    public static async Task<Guid> Handle(
        DefineMaintenanceItemCommand command,
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

        var (name, mileageWarning, timeWarningDays) = MaintenanceItemInput.NormalizeAndValidate(
            command.Name,
            command.MileageWarning,
            command.TimeWarningDays);

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
        await session.SaveChangesAsync(cancellationToken);

        return itemId;
    }
}
