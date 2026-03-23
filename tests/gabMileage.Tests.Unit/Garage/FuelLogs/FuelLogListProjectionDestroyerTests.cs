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

    [Fact]
    public void RecalculateMpgAndSort_FirstFillUp_ShouldHaveNullMpg()
    {
        var oldestId = Guid.NewGuid();
        var nextId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry
                {
                    FuelLogId = oldestId,
                    Date = new LocalDate(2026, 3, 1),
                    Odometer = 1000,
                    Gallons = 10m,
                    IsPartialFillUp = false,
                    CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0)
                },
                new FuelLogEntry
                {
                    FuelLogId = nextId,
                    Date = new LocalDate(2026, 3, 2),
                    Odometer = 1100,
                    Gallons = 10m,
                    IsPartialFillUp = false,
                    CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0)
                },
            ]
        };

        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == oldestId).Mpg.ShouldBeNull();
        list.Logs.Single(l => l.FuelLogId == nextId).Mpg.ShouldBe(10.0m);
    }

    [Fact]
    public void RecalculateMpgAndSort_PartialFillUp_ShouldSuppressMpg_ForCurrentAndNext()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var dId = Guid.NewGuid();
        var eId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = aId, Date = new LocalDate(2026, 3, 1), Odometer = 900, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0) },
                new FuelLogEntry { FuelLogId = bId, Date = new LocalDate(2026, 3, 2), Odometer = 1000, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0) },
                new FuelLogEntry { FuelLogId = cId, Date = new LocalDate(2026, 3, 3), Odometer = 1100, Gallons = 10m, IsPartialFillUp = true, CreatedAt = Instant.FromUtc(2026, 3, 3, 0, 0) },
                new FuelLogEntry { FuelLogId = dId, Date = new LocalDate(2026, 3, 4), Odometer = 1200, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 4, 0, 0) },
                new FuelLogEntry { FuelLogId = eId, Date = new LocalDate(2026, 3, 5), Odometer = 1300, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 5, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == aId).Mpg.ShouldBeNull("first fill-up suppressed");
        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBe(10.0m);

        // current partial suppressed
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBeNull();

        // immediately after partial suppressed (prior was partial)
        list.Logs.Single(l => l.FuelLogId == dId).Mpg.ShouldBeNull();

        // computation resumes after a full fill-up baseline is restored
        list.Logs.Single(l => l.FuelLogId == eId).Mpg.ShouldBe(10.0m);
    }

    [Fact]
    public void RecalculateMpgAndSort_WhenMiddleLogIsEdited_ShouldRecalculateDownstreamMpgUsingNewPriorBaseline()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = aId, Date = new LocalDate(2026, 3, 1), Odometer = 1000, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0) },
                new FuelLogEntry { FuelLogId = bId, Date = new LocalDate(2026, 3, 2), Odometer = 1100, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0) },
                new FuelLogEntry { FuelLogId = cId, Date = new LocalDate(2026, 3, 3), Odometer = 1200, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 3, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);
        list.Logs.Single(l => l.FuelLogId == aId).Mpg.ShouldBeNull();
        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBe(10.0m);
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBe(10.0m);

        // Edit the middle entry so it becomes a different baseline.
        var editedB = list.Logs.Single(l => l.FuelLogId == bId);
        editedB.Odometer = 1150;

        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBe(15.0m); // (1150 - 1000) / 10
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBe(5.0m);  // (1200 - 1150) / 10
    }

    [Fact]
    public void RecalculateMpgAndSort_WhenMiddleLogIsDeleted_ShouldRecalculateDownstreamMpgUsingNewPriorBaseline()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = aId, Date = new LocalDate(2026, 3, 1), Odometer = 1000, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0) },
                new FuelLogEntry { FuelLogId = bId, Date = new LocalDate(2026, 3, 2), Odometer = 1100, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0) },
                new FuelLogEntry { FuelLogId = cId, Date = new LocalDate(2026, 3, 3), Odometer = 1200, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 3, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);

        list.Logs.RemoveAll(l => l.FuelLogId == bId);
        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBe(20.0m); // (1200 - 1000) / 10
    }

    [Fact]
    public void RecalculateMpgAndSort_WhenLogBecomesPartial_ShouldSuppressItsMpg_AndTheNextLog()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = aId, Date = new LocalDate(2026, 3, 1), Odometer = 1000, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0) },
                new FuelLogEntry { FuelLogId = bId, Date = new LocalDate(2026, 3, 2), Odometer = 1100, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0) },
                new FuelLogEntry { FuelLogId = cId, Date = new LocalDate(2026, 3, 3), Odometer = 1200, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 3, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);
        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBe(10.0m);
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBe(10.0m);

        // Simulate edit: middle log becomes partial.
        list.Logs.Single(l => l.FuelLogId == bId).IsPartialFillUp = true;
        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBeNull();
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBeNull("immediately after a partial fill-up is suppressed");
    }

    [Fact]
    public void RecalculateMpgAndSort_WhenPartialLogIsDeleted_ShouldUnsuppressNextLogAndRebaselineToPriorFull()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = aId, Date = new LocalDate(2026, 3, 1), Odometer = 1000, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0) },
                new FuelLogEntry { FuelLogId = bId, Date = new LocalDate(2026, 3, 2), Odometer = 1100, Gallons = 10m, IsPartialFillUp = true, CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0) },
                new FuelLogEntry { FuelLogId = cId, Date = new LocalDate(2026, 3, 3), Odometer = 1200, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 3, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBeNull("prior was partial");

        // Delete the partial baseline.
        list.Logs.RemoveAll(l => l.FuelLogId == bId);
        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBe(20.0m); // (1200 - 1000) / 10
    }

    [Fact]
    public void RecalculateMpgAndSort_WhenEditReordersLogs_ShouldRecomputeMpgAgainstNewPrior()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = aId, Date = new LocalDate(2026, 3, 1), Odometer = 1000, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0) },
                new FuelLogEntry { FuelLogId = bId, Date = new LocalDate(2026, 3, 2), Odometer = 1100, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0) },
                new FuelLogEntry { FuelLogId = cId, Date = new LocalDate(2026, 3, 3), Odometer = 1200, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 3, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);
        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBe(10.0m);
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBe(10.0m);

        // Simulate edit: make B newest, changing C's prior baseline to A.
        var editedB = list.Logs.Single(l => l.FuelLogId == bId);
        editedB.Date = new LocalDate(2026, 3, 4);
        editedB.Odometer = 1300;

        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBe(20.0m); // (1200 - 1000) / 10
        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBe(10.0m); // (1300 - 1200) / 10
    }

    [Fact]
    public void RecalculateMpgAndSort_WhenGallonsIsZero_ShouldNotDivideByZero_AndShouldSuppressMpg()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry { FuelLogId = aId, Date = new LocalDate(2026, 3, 1), Odometer = 1000, Gallons = 10m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0) },
                new FuelLogEntry { FuelLogId = bId, Date = new LocalDate(2026, 3, 2), Odometer = 1100, Gallons = 0m, IsPartialFillUp = false, CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0) },
            ]
        };

        InvokeRecalculateMpgAndSort(list);
        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBeNull();
    }

    private static void InvokeRecalculateMpgAndSort(FuelLogList list)
    {
        var method = typeof(FuelLogListProjection)
            .GetMethod("RecalculateMpgAndSort", BindingFlags.NonPublic | BindingFlags.Static);

        method.ShouldNotBeNull("Expected FuelLogListProjection to have a private static RecalculateMpgAndSort(FuelLogList) helper.");
        method!.Invoke(null, [list]);
    }
}
