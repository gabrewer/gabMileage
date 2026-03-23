using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.Commands;
using gabMileage.Api.Garage.Vehicles.DomainEvents;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;
using Npgsql;

namespace gabMileage.Api.Garage.Vehicles.Handlers;

public static class AddVehicleHandler
{
    // Breadcrumbs (backend-builder)
    // What: Handle AddVehicleCommand by starting a new vehicle event stream and recording an idempotency marker.
    // Why: commandId is the required idempotency key; storing ProcessedAddVehicle in the same transaction makes duplicate submissions safe.
    // Alternatives considered: Idempotency via stream-only unique constraint (rejected: idempotency key is commandId, not vehicleId).
    // Confidence: high
    public static async Task<AddVehicleResult> Handle(
        AddVehicleCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth.UserId))
            throw new InvalidOperationException("Authenticated user is required.");

        if (command.CommandId == Guid.Empty)
            throw new VehicleValidationException(new Dictionary<string, string[]> { ["commandId"] = ["CommandId is required."] });

        var markerId = ProcessedAddVehicleId.From(auth.UserId, command.CommandId);

        // Idempotency check: if we've already processed this command id, no-op.
        var processed = await session.LoadAsync<ProcessedAddVehicle>(markerId, cancellationToken);
        if (processed is not null)
        {
            if (!string.Equals(processed.UserId, auth.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency marker user mismatch.");

            return new AddVehicleResult(processed.VehicleId, IsDuplicate: true);
        }

        var (name, make, model, year) = VehicleInput.NormalizeAndValidate(
            command.Name,
            command.Make,
            command.Model,
            command.Year,
            clock);

        var vehicleId = Guid.NewGuid();
        var streamId = VehicleStreamId.From(vehicleId);

        var now = clock.GetCurrentInstant();

        var added = new VehicleAdded(
            vehicleId,
            auth.UserId,
            command.CommandId,
            name,
            make,
            model,
            year,
            now);

        session.Events.StartStream<Vehicle>(streamId, added);

        // Persist the idempotency marker in the same transaction as the event stream start.
        session.Store(new ProcessedAddVehicle(
            markerId,
            auth.UserId,
            vehicleId,
            streamId,
            now));

        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return new AddVehicleResult(vehicleId, IsDuplicate: false);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // If two requests race, one may win storing ProcessedAddVehicle/starting the stream.
            // Treat the unique violation as idempotent success by re-loading the marker.
            var existing = await session.LoadAsync<ProcessedAddVehicle>(markerId, cancellationToken);
            if (existing is null)
                throw;

            if (!string.Equals(existing.UserId, auth.UserId, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency marker user mismatch.");

            return new AddVehicleResult(existing.VehicleId, IsDuplicate: true);
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
