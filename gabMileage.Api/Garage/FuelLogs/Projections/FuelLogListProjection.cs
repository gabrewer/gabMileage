using gabMileage.Api.Garage.FuelLogs;
using gabMileage.Api.Garage.FuelLogs.DomainEvents;
using gabMileage.Api.Garage.FuelLogs.ReadModels;
using JasperFx.Events.Projections;
using Marten.Events.Projections;

namespace gabMileage.Api.Garage.FuelLogs.Projections;

// Breadcrumbs (backend-builder)
// Who: backend-builder
// What: Project fuel log events (on vehicle streams) into a per-vehicle FuelLogList read model keyed by "fuellog-{vehicleId}".
// Why: Domain spec Decision D-01 + D-09: events live on vehicle-{vehicleId}, read model uses separate id prefix.
// Alternatives considered: Separate streams per fuel log (rejected); compute MPG at query time (rejected).
// Confidence: high
public sealed class FuelLogListProjection : MultiStreamProjection<FuelLogList, string>
{
    public FuelLogListProjection()
    {
        Lifecycle = ProjectionLifecycle.Inline;

        Identity<FuelLogRecorded>(e => FuelLogListId.From(e.VehicleId));
        Identity<FuelLogUpdated>(e => FuelLogListId.From(e.VehicleId));
        Identity<FuelLogDeleted>(e => FuelLogListId.From(e.VehicleId));

        ProjectEvent<FuelLogRecorded>((list, e) =>
        {
            list.Id = FuelLogListId.From(e.VehicleId);
            list.VehicleId = e.VehicleId;
            list.UserId = e.UserId;

            list.Logs.Add(new FuelLogEntry
            {
                FuelLogId = e.FuelLogId,
                Date = e.Date,
                Odometer = e.Odometer,
                Gallons = e.Gallons,
                TotalCost = e.TotalCost,
                PricePerGallon = e.PricePerGallon,
                CalculatedField = e.CalculatedField,
                IsPartialFillUp = e.IsPartialFillUp,
                Notes = e.Notes,
                CreatedAt = e.OccurredAt,
            });

            RecalculateMpgAndSort(list);
            return list;
        });

        ProjectEvent<FuelLogUpdated>((list, e) =>
        {
            var entry = list.Logs.FirstOrDefault(l => l.FuelLogId == e.FuelLogId);
            if (entry is not null)
            {
                entry.Date = e.Date;
                entry.Odometer = e.Odometer;
                entry.Gallons = e.Gallons;
                entry.TotalCost = e.TotalCost;
                entry.PricePerGallon = e.PricePerGallon;
                entry.CalculatedField = e.CalculatedField;
                entry.IsPartialFillUp = e.IsPartialFillUp;
                entry.Notes = e.Notes;
            }

            RecalculateMpgAndSort(list);
            return list;
        });

        ProjectEvent<FuelLogDeleted>((list, e) =>
        {
            list.Logs.RemoveAll(l => l.FuelLogId == e.FuelLogId);
            RecalculateMpgAndSort(list);
            return list;
        });
    }

    private static void RecalculateMpgAndSort(FuelLogList list)
    {
        // Sort chronologically for MPG calculation.
        var sorted = list.Logs
            .OrderBy(l => l.Date)
            .ThenBy(l => l.Odometer)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];

            if (i == 0)
            {
                current.Mpg = null;
                continue;
            }

            var prior = sorted[i - 1];

            if (current.IsPartialFillUp || prior.IsPartialFillUp)
            {
                current.Mpg = null;
                continue;
            }

            var distance = current.Odometer - prior.Odometer;
            if (distance <= 0 || current.Gallons <= 0)
            {
                current.Mpg = null;
                continue;
            }

            current.Mpg = Math.Round((decimal)distance / current.Gallons, 1);
        }

        list.Logs = sorted
            .OrderByDescending(l => l.Date)
            .ThenByDescending(l => l.Odometer)
            .ToList();

        list.LastOdometer = list.Logs.Count > 0 ? list.Logs[0].Odometer : null;
    }
}
