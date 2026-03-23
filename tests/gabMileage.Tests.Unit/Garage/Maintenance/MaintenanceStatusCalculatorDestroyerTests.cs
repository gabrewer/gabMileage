using gabMileage.Api.Garage.Maintenance;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceStatusCalculatorDestroyerTests
{
    [Fact]
    public void Calculate_WhenTimeDueSoonAboutAWeek_ShouldRenderAs1Week_PerSprint04Examples()
    {
        // Sprint 04 brief + domain doc examples explicitly show: "Due in 1 week".
        // This test sets up a time-only item that is due soon with ~7 days remaining.
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Tire Rotation",
            MileageInterval = null,
            MileageWarning = 500,
            TimeIntervalMonths = 1,           // 1 * 30.44 days
            TimeWarningDays = 14,
            LastPerformedDate = new LocalDate(2026, 2, 28), // 23 days before 2026-03-23
            LastPerformedOdometer = null,
            Records = [],
        };

        var (status, detail, _, _) = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: null,
            currentDate: new LocalDate(2026, 3, 23));

        status.ShouldBe("dueSoon");
        detail.ShouldBe("Due in 1 week");
    }

    [Fact]
    public void Calculate_WhenMileageOkButTimeDueSoon_ShouldReturnDueSoon_WithTimeDetail()
    {
        // Combined intervals: worse status wins, and detail should come from the worse dimension.
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Oil Change",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = 1,
            TimeWarningDays = 14,
            LastPerformedDate = new LocalDate(2026, 2, 28),
            LastPerformedOdometer = 10_000,
            Records = [],
        };

        // Mileage OK: only 2,200 miles since service.
        var (status, detail, _, _) = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: 12_200,
            currentDate: new LocalDate(2026, 3, 23));

        status.ShouldBe("dueSoon");
        detail.ShouldBe("Due in 1 week");
    }

    [Fact]
    public void Calculate_WhenBothMileageAndTimeAreDueSoon_ShouldIncludeBothParts_UsingWeekFormatting()
    {
        // Sprint 04 brief example: "Due in ~450 mi or 1 week".
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Oil Change",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = 1,
            TimeWarningDays = 14,
            LastPerformedDate = new LocalDate(2026, 2, 28),
            LastPerformedOdometer = 10_000,
            Records = [],
        };

        // Mileage Due Soon: milesSince=4,550 => due in 450.
        var (status, detail, _, _) = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: 14_550,
            currentDate: new LocalDate(2026, 3, 23));

        status.ShouldBe("dueSoon");
        detail.ShouldBe("Due in ~450 mi or 1 week");
    }

    [Fact]
    public void Calculate_WhenTimeOverdueByAboutThreeWeeks_ShouldRenderAs3Weeks()
    {
        // Matches Sprint 04 example: "Overdue by 3 weeks".
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Brake Fluid",
            MileageInterval = null,
            MileageWarning = 500,
            TimeIntervalMonths = 1,
            TimeWarningDays = 14,
            LastPerformedDate = new LocalDate(2026, 1, 31),
            LastPerformedOdometer = null,
            Records = [],
        };

        var (status, detail, _, _) = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: null,
            currentDate: new LocalDate(2026, 3, 23));

        status.ShouldBe("overdue");
        detail.ShouldBe("Overdue by 3 weeks");
    }
}
