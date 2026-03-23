using System.Reflection;
using gabMileage.Api.Garage.FuelLogs.Projections;
using gabMileage.Api.Garage.FuelLogs.ReadModels;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.FuelLogs;

public sealed class FuelLogListProjectionTask007Tests
{
    [Fact]
    public void TASK_007_RecalculateMpgAndSort_ShouldSuppressAroundPartial_AndRoundMpgHalfUpAtMidpoint()
    {
        // Scenario:
        // - A: first fill-up => MPG suppressed
        // - B: partial fill-up => MPG suppressed
        // - C: full fill-up immediately after partial => MPG suppressed (adjacent rule)
        // - D: full fill-up after baseline restored => MPG computed
        //   MPG uses midpoint rounding: 1.25 should round to 1.3 (half-up / away-from-zero), not 1.2 (banker's).

        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var dId = Guid.NewGuid();

        var list = new FuelLogList
        {
            Logs =
            [
                new FuelLogEntry
                {
                    FuelLogId = aId,
                    Date = new LocalDate(2026, 3, 1),
                    Odometer = 1000,
                    Gallons = 10m,
                    IsPartialFillUp = false,
                    CreatedAt = Instant.FromUtc(2026, 3, 1, 0, 0)
                },
                new FuelLogEntry
                {
                    FuelLogId = bId,
                    Date = new LocalDate(2026, 3, 2),
                    Odometer = 1100,
                    Gallons = 10m,
                    IsPartialFillUp = true,
                    CreatedAt = Instant.FromUtc(2026, 3, 2, 0, 0)
                },
                new FuelLogEntry
                {
                    FuelLogId = cId,
                    Date = new LocalDate(2026, 3, 3),
                    Odometer = 2000,
                    Gallons = 10m,
                    IsPartialFillUp = false,
                    CreatedAt = Instant.FromUtc(2026, 3, 3, 0, 0)
                },
                new FuelLogEntry
                {
                    FuelLogId = dId,
                    Date = new LocalDate(2026, 3, 4),
                    Odometer = 2125,
                    Gallons = 100m,
                    IsPartialFillUp = false,
                    CreatedAt = Instant.FromUtc(2026, 3, 4, 0, 0)
                },
            ]
        };

        InvokeRecalculateMpgAndSort(list);

        list.Logs.Single(l => l.FuelLogId == aId).Mpg.ShouldBeNull();
        list.Logs.Single(l => l.FuelLogId == bId).Mpg.ShouldBeNull();
        list.Logs.Single(l => l.FuelLogId == cId).Mpg.ShouldBeNull();

        // Midpoint rounding expectation (125 / 100 = 1.25 => 1.3).
        // NOTE: Math.Round(decimal, 1) defaults to MidpointRounding.ToEven, which would yield 1.2.
        list.Logs.Single(l => l.FuelLogId == dId).Mpg.ShouldBe(1.3m);

        // Display order still newest-first.
        list.Logs[0].FuelLogId.ShouldBe(dId);
    }

    private static void InvokeRecalculateMpgAndSort(FuelLogList list)
    {
        var method = typeof(FuelLogListProjection)
            .GetMethod("RecalculateMpgAndSort", BindingFlags.NonPublic | BindingFlags.Static);

        method.ShouldNotBeNull("Expected FuelLogListProjection to have a private static RecalculateMpgAndSort(FuelLogList) helper.");
        method!.Invoke(null, [list]);
    }
}
