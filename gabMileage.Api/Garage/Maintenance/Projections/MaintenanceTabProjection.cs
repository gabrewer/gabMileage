using NodaTime;
using gabMileage.Api.Garage.Maintenance;
using gabMileage.Api.Garage.FuelLogs.DomainEvents;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using JasperFx.Events.Projections;
using Marten.Events.Projections;

namespace gabMileage.Api.Garage.Maintenance.Projections;

// Breadcrumbs (backend-builder)
// Who: backend-builder
// What: Project maintenance events (on vehicle streams) into a per-vehicle MaintenanceTabData read model keyed by "maintenance-{vehicleId}".
// Why: TASK-001 acceptance criteria #2/#3 require immediate materialization and full removal from the tab model.
// Alternatives considered: Build maintenance tab on-the-fly by querying event store (rejected: slow + complex for UI).
// Confidence: high
public sealed class MaintenanceTabProjection : MultiStreamProjection<MaintenanceTabData, string>
{
    public MaintenanceTabProjection()
    {
        Lifecycle = ProjectionLifecycle.Inline;

        Identity<MaintenanceItemDefined>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenanceItemUpdated>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenanceItemRemoved>(e => MaintenanceTabDataId.From(e.VehicleId));

        Identity<MaintenancePerformed>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenanceRecordUpdated>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<MaintenanceRecordDeleted>(e => MaintenanceTabDataId.From(e.VehicleId));

        // TASK-003: include fuel log events so MaintenanceTabData.LastOdometer can reflect the newest reading from any source.
        Identity<FuelLogRecorded>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<FuelLogUpdated>(e => MaintenanceTabDataId.From(e.VehicleId));
        Identity<FuelLogDeleted>(e => MaintenanceTabDataId.From(e.VehicleId));

        ProjectEvent<MaintenanceItemDefined>((data, e) =>
        {
            data.Id = MaintenanceTabDataId.From(e.VehicleId);
            data.VehicleId = e.VehicleId;
            data.UserId = e.UserId;

            data.Items.Add(new MaintenanceItemEntry
            {
                ItemId = e.ItemId,
                Name = e.Name,
                MileageInterval = e.MileageInterval,
                TimeIntervalMonths = e.TimeIntervalMonths,
                MileageWarning = e.MileageWarning,
                TimeWarningDays = e.TimeWarningDays,
                LastPerformedDate = e.InitialLastPerformedDate,
                LastPerformedOdometer = e.InitialLastPerformedOdometer,
                Records = [],
            });

            RecalculateLastOdometer(data);
            return data;
        });

        ProjectEvent<MaintenanceItemUpdated>((data, e) =>
        {
            var item = data.Items.FirstOrDefault(i => i.ItemId == e.ItemId);
            if (item is null)
                return data;

            item.Name = e.Name;
            item.MileageInterval = e.MileageInterval;
            item.TimeIntervalMonths = e.TimeIntervalMonths;
            item.MileageWarning = e.MileageWarning;
            item.TimeWarningDays = e.TimeWarningDays;

            return data;
        });

        ProjectEvent<MaintenanceItemRemoved>((data, e) =>
        {
            data.Items.RemoveAll(i => i.ItemId == e.ItemId);
            RecalculateLastOdometer(data);
            return data;
        });

        ProjectEvent<MaintenancePerformed>((data, e) =>
        {
            var item = data.Items.FirstOrDefault(i => i.ItemId == e.ItemId);
            if (item is null)
                return data;

            item.Records.Add(new MaintenanceRecordEntry
            {
                RecordId = e.RecordId,
                ItemId = e.ItemId,
                Date = e.Date,
                Odometer = e.Odometer,
                Notes = e.Notes,
                CreatedAt = e.OccurredAt,
            });

            item.Records = item.Records
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.Odometer)
                .ToList();

            // Keep LastPerformed* as the initial baseline seeded on item creation.
            // Effective "last performed" is derived from Records at query time.
            RecalculateLastOdometer(data);
            return data;
        });

        ProjectEvent<MaintenanceRecordUpdated>((data, e) =>
        {
            var item = data.Items.FirstOrDefault(i => i.ItemId == e.ItemId);
            var record = item?.Records.FirstOrDefault(r => r.RecordId == e.RecordId);
            if (record is null)
                return data;

            record.Date = e.Date;
            record.Odometer = e.Odometer;
            record.Notes = e.Notes;

            item!.Records = item.Records
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.Odometer)
                .ToList();

            RecalculateLastOdometer(data);
            return data;
        });

        ProjectEvent<MaintenanceRecordDeleted>((data, e) =>
        {
            var item = data.Items.FirstOrDefault(i => i.ItemId == e.ItemId);
            if (item is null)
                return data;

            item.Records.RemoveAll(r => r.RecordId == e.RecordId);

            item.Records = item.Records
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.Odometer)
                .ToList();

            // Keep LastPerformed* as the initial baseline seeded on item creation.
            // Effective "last performed" is derived from Records at query time.
            RecalculateLastOdometer(data);
            return data;
        });

        // Breadcrumbs (backend-builder)
        // Who: backend-builder
        // What: Subscribe to FuelLog* events to keep MaintenanceTabData.LastOdometer updated from fuel entries.
        // Why: TASK-003 requires lastOdometer to reflect the newest reading from any source; projection tracks a max so query can reconcile.
        // Alternatives considered: Loading FuelLogList inside projection (rejected: projections must be pure/side-effect free).
        // Confidence: high
        ProjectEvent<FuelLogRecorded>((data, e) =>
        {
            data.Id = MaintenanceTabDataId.From(e.VehicleId);
            data.VehicleId = e.VehicleId;
            if (string.IsNullOrWhiteSpace(data.UserId))
                data.UserId = e.UserId;

            RecalculateLastOdometerFromFuelAndMaintenance(data, e.Date, e.Odometer);
            return data;
        });

        ProjectEvent<FuelLogUpdated>((data, e) =>
        {
            data.Id = MaintenanceTabDataId.From(e.VehicleId);
            data.VehicleId = e.VehicleId;
            if (string.IsNullOrWhiteSpace(data.UserId))
                data.UserId = e.UserId;

            RecalculateLastOdometerFromFuelAndMaintenance(data, e.Date, e.Odometer);
            return data;
        });

        ProjectEvent<FuelLogDeleted>((data, e) =>
        {
            // Cannot determine new fuel-side max from deletion alone.
            // RecalculateLastOdometer will use maintenance records only.
            // FuelLogList.LastOdometer is the source of truth for fuel — we reconcile at query time.
            RecalculateLastOdometer(data);
            return data;
        });
    }

    private readonly record struct OdometerReading(LocalDate Date, int Odometer);

    private static OdometerReading? GetLatestMaintenanceReading(MaintenanceTabData data)
    {
        // Baseline counts as an odometer source for the tab (even with no records yet).
        // If the baseline date is missing, treat it as the oldest possible date so any real log wins.
        var candidates = data.Items
            .SelectMany(i => i.Records)
            .Select(r => new OdometerReading(r.Date, r.Odometer))
            .Concat(data.Items
                .Where(i => i.LastPerformedOdometer.HasValue)
                .Select(i => new OdometerReading(i.LastPerformedDate ?? LocalDate.MinIsoValue, i.LastPerformedOdometer!.Value)));

        var hasAny = false;
        OdometerReading latest = default;

        foreach (var candidate in candidates)
        {
            if (!hasAny)
            {
                latest = candidate;
                hasAny = true;
                continue;
            }

            if (candidate.Date > latest.Date || (candidate.Date == latest.Date && candidate.Odometer > latest.Odometer))
                latest = candidate;
        }

        return hasAny ? latest : null;
    }

    private static void RecalculateLastOdometer(MaintenanceTabData data)
    {
        // Maintenance-side only; fuel log reading is reconciled at query time.
        var latest = GetLatestMaintenanceReading(data);
        data.LastOdometer = latest is { Odometer: > 0 } r ? r.Odometer : null;
    }

    private static void RecalculateLastOdometerFromFuelAndMaintenance(
        MaintenanceTabData data,
        LocalDate fuelDate,
        int fuelOdometer)
    {
        var maintenance = GetLatestMaintenanceReading(data);
        var fuel = new OdometerReading(fuelDate, fuelOdometer);

        var latest = maintenance is null
            ? fuel
            : (fuel.Date > maintenance.Value.Date || (fuel.Date == maintenance.Value.Date && fuel.Odometer > maintenance.Value.Odometer))
                ? fuel
                : maintenance.Value;

        data.LastOdometer = latest.Odometer > 0 ? latest.Odometer : null;
    }
}
