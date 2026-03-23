using System.Reflection;
using gabMileage.Api.Garage.FuelLogs.Projections;
using gabMileage.Api.Garage.FuelLogs.ReadModels;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.FuelLogs;

public sealed class FuelLogListProjectionDestroyerTests
{
    [Fact]
    public void RecalculateMpgAndSort_ShouldSortByDateDesc_ThenOdometerDesc_ForSameDateTies()
    {
        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = Guid.NewGuid(), Date = new LocalDate(2026, 3, 10), Odometer = 1000, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 10, 0, 0) },
                new FuelLogEntry { FuelLogId = Guid.NewGuid(), Date = new LocalDate(2026, 3, 11), Odometer = 900, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 11, 0, 0) },
                new FuelLogEntry { FuelLogId = Guid.NewGuid(), Date = new LocalDate(2026, 3, 11), Odometer = 1100, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 11, 1, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);

        list.Logs.Select(l => (l.Date, l.Odometer)).ShouldBe(
        [
            (new LocalDate(2026, 3, 11), 1100),
            (new LocalDate(2026, 3, 11), 900),
            (new LocalDate(2026, 3, 10), 1000),
        ]);

        list.LastOdometer.ShouldBe(1100);
    }

    [Fact]
    public void LastOdometer_ShouldPreferNewestDate_EvenIfOlderEntryHasHigherOdometer()
    {
        // Attack: If someone accidentally computes lastOdometer as Max(Odometer),
        // a back-dated but higher mileage entry would incorrectly win.
        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = Guid.NewGuid(), Date = new LocalDate(2026, 3, 10), Odometer = 9999, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 10, 0, 0) },
                new FuelLogEntry { FuelLogId = Guid.NewGuid(), Date = new LocalDate(2026, 3, 11), Odometer = 100, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 11, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);

        list.Logs[0].Date.ShouldBe(new LocalDate(2026, 3, 11));
        list.LastOdometer.ShouldBe(100);
    }

    [Fact]
    public void LastOdometer_ShouldRecalculate_WhenMostRecentLogIsRemoved()
    {
        var newestId = Guid.NewGuid();
        var secondNewestId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = newestId, Date = new LocalDate(2026, 3, 11), Odometer = 1100, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 11, 1, 0) },
                new FuelLogEntry { FuelLogId = secondNewestId, Date = new LocalDate(2026, 3, 11), Odometer = 900, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 11, 0, 0) },
                new FuelLogEntry { FuelLogId = Guid.NewGuid(), Date = new LocalDate(2026, 3, 10), Odometer = 1000, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 10, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);
        list.LastOdometer.ShouldBe(1100);

        // Simulate the delete projection behavior: remove by FuelLogId then recalc/sort.
        list.Logs.RemoveAll(l => l.FuelLogId == newestId);
        InvokeRecalculateMpgAndSort(list);

        list.Logs[0].FuelLogId.ShouldBe(secondNewestId);
        list.LastOdometer.ShouldBe(900);
    }

    [Fact]
    public void RecalculateMpgAndSort_ShouldReorder_WhenEntryIsUpdatedToNewerDateAndHigherOdometer()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = aId, Date = new LocalDate(2026, 3, 10), Odometer = 1000, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 10, 0, 0) },
                new FuelLogEntry { FuelLogId = bId, Date = new LocalDate(2026, 3, 11), Odometer = 900, Gallons = 10m, CreatedAt = Instant.FromUtc(2026, 3, 11, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);
        list.Logs[0].FuelLogId.ShouldBe(bId);
        list.LastOdometer.ShouldBe(900);

        // Attack: update an older entry to become newest; projection must re-sort and update lastOdometer.
        var entryA = list.Logs.First(l => l.FuelLogId == aId);
        entryA.Date = new LocalDate(2026, 3, 12);
        entryA.Odometer = 1500;

        InvokeRecalculateMpgAndSort(list);

        list.Logs[0].FuelLogId.ShouldBe(aId);
        list.LastOdometer.ShouldBe(1500);
    }

    private static void InvokeRecalculateMpgAndSort(FuelLogList list)
    {
        var method = typeof(FuelLogListProjection)
            .GetMethod("RecalculateMpgAndSort", BindingFlags.NonPublic | BindingFlags.Static);

        method.ShouldNotBeNull("Expected FuelLogListProjection to have a private static RecalculateMpgAndSort(FuelLogList) helper.");
        method!.Invoke(null, [list]);
    }
}
