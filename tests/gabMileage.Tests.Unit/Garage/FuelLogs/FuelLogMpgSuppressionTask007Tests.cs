using System.Reflection;
using gabMileage.Api.Garage.FuelLogs.Projections;
using gabMileage.Api.Garage.FuelLogs.ReadModels;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.FuelLogs;

public sealed class FuelLogMpgSuppressionTask007Tests
{
    [Fact]
    public void RecalculateMpgAndSort_WhenPartialIsCleared_ShouldRecalculateCurrentAndNextMpg()
    {
        // Scenario:
        // A(full) -> B(partial) -> C(full)
        // Clearing B's partial flag should unsuppress B and C and recompute MPG using adjacent baselines.

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

        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBeNull("partial fill-up suppresses its MPG");
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBeNull("adjacent after a partial is also suppressed");

        // Edit: B becomes a full fill-up.
        list.Logs.Single(l => l.FuelLogId == bId).IsPartialFillUp = false;
        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBe(10.0m); // (1100 - 1000) / 10
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBe(10.0m); // (1200 - 1100) / 10
    }

    private static void InvokeRecalculateMpgAndSort(FuelLogList list)
    {
        var method = typeof(FuelLogListProjection)
            .GetMethod("RecalculateMpgAndSort", BindingFlags.NonPublic | BindingFlags.Static);

        method.ShouldNotBeNull("Expected FuelLogListProjection to have a private static RecalculateMpgAndSort(FuelLogList) helper.");
        method!.Invoke(null, [list]);
    }
}
