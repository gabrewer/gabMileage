using gabMileage.Api.Garage.Vehicles.DomainEvents;

namespace gabMileage.Api.Garage.Vehicles;

// Breadcrumbs (backend-builder)
// What: Event-sourced Vehicle aggregate root.
// Why: Wolverine/Marten StartStream<T>() uses an aggregate type; aggregate also provides a place for write-side invariants later.
// Alternatives considered: No aggregate type and using StartStream<object> (rejected: loses domain boundary).
// Confidence: medium
public sealed class Vehicle
{
    public Guid VehicleId { get; private set; }
    public string UserId { get; private set; } = string.Empty;

    public void Apply(VehicleAdded e)
    {
        VehicleId = e.VehicleId;
        UserId = e.UserId;
    }

    public void Apply(VehicleUpdated e)
    {
        // Ownership is immutable after VehicleAdded. VehicleUpdated should not change UserId.
    }
}
