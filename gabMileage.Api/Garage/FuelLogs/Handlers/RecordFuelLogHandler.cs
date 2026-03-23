using System.Globalization;
using gabMileage.Api.Auth;
using gabMileage.Api.Garage.FuelLogs.Commands;
using gabMileage.Api.Garage.FuelLogs.DomainEvents;
using gabMileage.Api.Garage.FuelLogs.ReadModels;
using gabMileage.Api.Garage.FuelLogs.Validation;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;
using Npgsql;

namespace gabMileage.Api.Garage.FuelLogs.Handlers;

public static class RecordFuelLogHandler
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Handle RecordFuelLogCommand by appending FuelLogRecorded to the vehicle-{vehicleId} stream.
    // Why: Sprint 03 Decision D-01 keeps fuel log events on the vehicle stream; idempotency is required via commandId.
    // Alternatives considered: Separate fuellog-{fuelLogId} streams (rejected per domain spec).
    // Confidence: high
    public static async Task<RecordFuelLogResult> Handle(
        RecordFuelLogCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth.UserId))
            throw new InvalidOperationException("Authenticated user is required.");

        if (command.CommandId == Guid.Empty)
            throw new FuelLogValidationException(new Dictionary<string, string[]> { ["commandId"] = ["CommandId is required."] });

        if (command.VehicleId == Guid.Empty)
            throw new FuelLogValidationException(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

        var markerId = ProcessedRecordFuelLogId.From(auth.UserId, command.CommandId);

        // Idempotency check
        var processed = await session.LoadAsync<ProcessedRecordFuelLog>(markerId, cancellationToken);
        if (processed is not null)
        {
            if (!string.Equals(processed.UserId, auth.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency marker user mismatch.");

            return new RecordFuelLogResult(processed.FuelLogId, IsDuplicate: true, OdometerWarning: null);
        }

        // Ownership check (vehicle)
        var vehicleStreamId = VehicleStreamId.From(command.VehicleId);
        var vehicle = await session.LoadAsync<VehicleReadModel>(vehicleStreamId, cancellationToken);
        if (vehicle is null || !string.Equals(vehicle.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        // Validate other fields
        FuelLogInput.Validate(command.Date, command.Odometer, command.Notes, clock);

        // Validate/normalize fuel fields
        var (gallons, totalCost, pricePerGallon, calculatedField) = FuelFieldCalculator.NormalizeAndValidate(
            command.Gallons,
            command.TotalCost,
            command.PricePerGallon,
            command.CalculatedField);

        // Optional warning: odometer lower than most recent prior reading (not an error)
        string? odometerWarning = null;
        var existingList = await session.LoadAsync<FuelLogList>(FuelLogListId.From(command.VehicleId), cancellationToken);
        if (existingList?.LastOdometer is int last && command.Odometer < last)
        {
            var formatted = last.ToString("N0", CultureInfo.InvariantCulture);
            odometerWarning = $"This reading is lower than your last one ({formatted} mi). Double-check?";
        }

        var fuelLogId = Guid.NewGuid();
        var now = clock.GetCurrentInstant();

        var recorded = new FuelLogRecorded(
            fuelLogId,
            command.VehicleId,
            auth.UserId,
            command.CommandId,
            command.Date,
            command.Odometer,
            gallons,
            totalCost,
            pricePerGallon,
            calculatedField,
            command.IsPartialFillUp,
            string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            now);

        session.Events.Append(vehicleStreamId, recorded);

        // Persist the idempotency marker in the same transaction as the event append.
        session.Insert(new ProcessedRecordFuelLog(markerId, auth.UserId, fuelLogId, now));

        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return new RecordFuelLogResult(fuelLogId, IsDuplicate: false, OdometerWarning: odometerWarning);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // If two requests race, treat unique violation as idempotent success.
            var existing = await session.LoadAsync<ProcessedRecordFuelLog>(markerId, cancellationToken);
            if (existing is null)
                throw;

            return new RecordFuelLogResult(existing.FuelLogId, IsDuplicate: true, OdometerWarning: null);
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
