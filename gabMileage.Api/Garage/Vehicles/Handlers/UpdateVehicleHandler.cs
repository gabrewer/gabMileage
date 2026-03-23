using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.Commands;
using gabMileage.Api.Garage.Vehicles.DomainEvents;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using Marten;
using NodaTime;

namespace gabMileage.Api.Garage.Vehicles.Handlers;

public static class UpdateVehicleHandler
{
    // Breadcrumbs (backend-builder)
    // What: Handle UpdateVehicleCommand by appending VehicleUpdated to the existing vehicle stream.
    // Why: Event sourcing requires append-only history; read model updates via inline projection.
    // Alternatives considered: Directly updating VehicleReadModel (rejected: bypasses event store).
    // Confidence: high
    public static async Task Handle(
        UpdateVehicleCommand command,
        AuthContext auth,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth.UserId))
            throw new InvalidOperationException("Authenticated user is required.");

        if (command.VehicleId == Guid.Empty)
            throw new VehicleValidationException(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

        var (name, make, model, year) = VehicleInput.NormalizeAndValidate(
            command.Name,
            command.Make,
            command.Model,
            command.Year,
            clock);

        var streamId = VehicleStreamId.From(command.VehicleId);

        var existing = await session.LoadAsync<VehicleReadModel>(streamId, cancellationToken);
        if (existing is null || !string.Equals(existing.UserId, auth.UserId, StringComparison.Ordinal))
            throw new VehicleNotFoundException(command.VehicleId);

        var now = clock.GetCurrentInstant();

        session.Events.Append(streamId, new VehicleUpdated(
            command.VehicleId,
            auth.UserId,
            name,
            make,
            model,
            year,
            now));

        await session.SaveChangesAsync(cancellationToken);
    }
}
