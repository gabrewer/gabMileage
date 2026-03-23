using gabMileage.Api.Garage.Vehicles.DomainEvents;
using NodaTime;

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

    // Sprint 02 profile fields (mirrors VehicleAdded/VehicleUpdated payload).
    public Guid CommandId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Make { get; private set; }
    public string? Model { get; private set; }
    public int? Year { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    public void Apply(VehicleAdded e)
    {
        VehicleId = e.VehicleId;

        // Avoid assigning ownership inside VehicleUpdated.Apply; ownership is immutable post-creation.
        var userId = e.UserId;
        UserId = userId;

        CommandId = e.CommandId;

        Name = e.Name;
        Make = e.Make;
        Model = e.Model;
        Year = e.Year;

        CreatedAt = e.OccurredAt;
        UpdatedAt = e.OccurredAt;
    }

    public void Apply(VehicleUpdated e)
    {
        // Ownership, CreatedAt, and CommandId are immutable after VehicleAdded.
        Name = e.Name;
        Make = e.Make;
        Model = e.Model;
        Year = e.Year;
        UpdatedAt = e.OccurredAt;
    }
}
