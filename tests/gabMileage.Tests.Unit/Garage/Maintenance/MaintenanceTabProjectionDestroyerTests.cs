using System.Reflection;
using gabMileage.Api.Garage.Maintenance.Projections;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceTabProjectionDestroyerTests
{
    [Fact]
    public void RecalculateLastOdometer_ShouldInclude_InitialBaseline_WhenNoRecordsExist()
    {
        // Attack: if the user defines an item with an initial last-performed odometer,
        // lastOdometer must not be null — otherwise mileage-based status calculations
        // incorrectly behave as if there is no odometer data at all.
        var data = new MaintenanceTabData
        {
            Items =
            [
                new MaintenanceItemEntry
                {
                    ItemId = Guid.NewGuid(),
                    Name = "Oil Change",
                    LastPerformedDate = new LocalDate(2026, 03, 01),
                    LastPerformedOdometer = 42_000,
                    Records = []
                }
            ]
        };

        InvokePrivateStatic("RecalculateLastOdometer", data);

        data.LastOdometer.ShouldBe(42_000);
    }

    [Fact]
    public void RecalculateLastOdometer_ShouldUse_MostRecentMaintenanceRecord_ByDateNotMaxOdometer()
    {
        // Attack: The API contract calls lastOdometer the *most recent* reading.
        // If the system allows backdated edits (or incorrect odometer entry), taking numeric Max()
        // will select an older record and inflate current mileage, causing incorrect "overdue" ordering.
        var itemId = Guid.NewGuid();

        var olderButHigher = new MaintenanceRecordEntry
        {
            RecordId = Guid.NewGuid(),
            ItemId = itemId,
            Date = new LocalDate(2026, 01, 01),
            Odometer = 10_000,
            Notes = null,
            CreatedAt = Instant.FromUtc(2026, 01, 01, 0, 0)
        };

        var newerButLower = new MaintenanceRecordEntry
        {
            RecordId = Guid.NewGuid(),
            ItemId = itemId,
            Date = new LocalDate(2026, 02, 01),
            Odometer = 9_000,
            Notes = null,
            CreatedAt = Instant.FromUtc(2026, 02, 01, 0, 0)
        };

        var data = new MaintenanceTabData
        {
            Items =
            [
                new MaintenanceItemEntry
                {
                    ItemId = itemId,
                    Name = "Air Filter",
                    Records = [olderButHigher, newerButLower]
                }
            ]
        };

        InvokePrivateStatic("RecalculateLastOdometer", data);

        var expectedMostRecent = data.Items
            .SelectMany(i => i.Records)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Odometer)
            .First()
            .Odometer;

        data.LastOdometer.ShouldBe(expectedMostRecent);
    }

    private static void InvokePrivateStatic(string methodName, MaintenanceTabData data)
    {
        var method = typeof(MaintenanceTabProjection)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        method.ShouldNotBeNull($"Expected to find private static method '{methodName}' on {nameof(MaintenanceTabProjection)}.");

        method!.Invoke(null, new object?[] { data });
    }
}
