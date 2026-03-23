using gabMileage.Api.Garage.Vehicles.DomainEvents;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using JasperFx.Events.Projections;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace gabMileage.Api.Garage.Vehicles.Projections;

// Breadcrumbs (backend-builder)
// What: Inline single-stream projection from Vehicle events into VehicleReadModel.
// Why: Acceptance criteria require read-after-write consistency (ProjectionLifecycle.Inline).
// Alternatives considered: Async projections/daemon (rejected: eventual consistency breaks tests and UX).
// Confidence: high
public sealed class VehicleProjection : SingleStreamProjection<VehicleReadModel, string>
{
    public VehicleProjection()
    {
        Lifecycle = ProjectionLifecycle.Inline;

        CreateEvent<VehicleAdded>(Create);

        ProjectEvent<VehicleUpdated>((rm, e) =>
        {
            rm.Name = e.Name;
            rm.Make = e.Make;
            rm.Model = e.Model;
            rm.Year = e.Year;
            rm.UpdatedAt = e.OccurredAt;
        });
    }

    public VehicleReadModel Create(VehicleAdded e) => new()
    {
        StreamId = VehicleStreamId.From(e.VehicleId),
        Id = e.VehicleId,
        UserId = e.UserId,
        Name = e.Name,
        Make = e.Make,
        Model = e.Model,
        Year = e.Year,
        PhotoUrl = null,
        PhotoThumbnailUrl = null,
        CreatedAt = e.OccurredAt,
        UpdatedAt = e.OccurredAt,
    };
}
