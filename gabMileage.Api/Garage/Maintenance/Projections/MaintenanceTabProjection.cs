using gabMileage.Api.Garage.Maintenance;
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
    }

    private static void RecalculateLastOdometer(MaintenanceTabData data)
    {
        data.LastOdometer = data.Items
            .SelectMany(i => i.Records)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Odometer)
            .Select(r => (int?)r.Odometer)
            .FirstOrDefault();
    }
}
