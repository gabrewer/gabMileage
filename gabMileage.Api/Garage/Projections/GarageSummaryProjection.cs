using gabMileage.Api.Garage.ReadModels;
using gabMileage.Api.Garage.Vehicles.DomainEvents;
using JasperFx.Events.Projections;
using Marten.Events.Projections;

namespace gabMileage.Api.Garage.Projections;

// Breadcrumbs (backend-builder)
// Who: backend-builder
// What: Project vehicle events into a per-user GarageSummary read model (MultiStreamProjection keyed by UserId).
// Why: GET /api/v1/garage/summary must be query-side, projection-backed, and return vehicles newest-first.
// Alternatives considered: Query VehicleReadModel and shape on demand (rejected: summary wants a single load + stable contract).
// Confidence: medium
public sealed class GarageSummaryProjection : MultiStreamProjection<GarageSummary, string>
{
    public GarageSummaryProjection()
    {
        // Route vehicle events from many streams into a single summary document per user.
        // Lifecycle is configured at registration time (Program.cs) to avoid accidental inline concurrency hotspots.
        Identity<VehicleAdded>(e => e.UserId);
        Identity<VehicleUpdated>(e => e.UserId);

        ProjectEvent<VehicleAdded>((summary, e) =>
        {
            summary.UserId = e.UserId;

            var vehicles = summary.Vehicles?.ToList() ?? [];

            // Newest-first: insert at index 0 so the list is naturally descending by CreatedAt.
            vehicles.Insert(0, new GarageSummaryVehicle
            {
                VehicleId = e.VehicleId,
                Name = e.Name,
                Make = e.Make,
                Model = e.Model,
                Year = e.Year,
                PhotoUrl = null,
                PhotoThumbnailUrl = null,
                CreatedAt = e.OccurredAt,
            });

            // Defensive ordering: out-of-order event replay or clock skew should not break newest-first invariant.
            summary.Vehicles = vehicles
                .OrderByDescending(v => v.CreatedAt)
                .ToList();
            return summary;
        });

        ProjectEvent<VehicleUpdated>((summary, e) =>
        {
            var vehicles = summary.Vehicles?.ToList() ?? [];
            var existing = vehicles.FirstOrDefault(v => v.VehicleId == e.VehicleId);
            if (existing is null)
            {
                // Safety: if historical events are replayed out of order, don't blow up projections.
                summary.Vehicles = vehicles
                    .OrderByDescending(v => v.CreatedAt)
                    .ToList();
                return summary;
            }

            existing.Name = e.Name;
            existing.Make = e.Make;
            existing.Model = e.Model;
            existing.Year = e.Year;

            // Re-sort after mutation to persist newest-first ordering.
            summary.Vehicles = vehicles
                .OrderByDescending(v => v.CreatedAt)
                .ToList();
            return summary;
        });
    }
}
