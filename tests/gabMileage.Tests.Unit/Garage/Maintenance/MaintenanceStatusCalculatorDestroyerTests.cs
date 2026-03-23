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

    [Fact]
    public void Calculate_WhenNeverPerformedAndNoInitialBaseline_ShouldReturnDueSoon_WithNullLastPerformed()
    {
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Oil Change",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = null,
            TimeWarningDays = 14,
            LastPerformedDate = null,
            LastPerformedOdometer = null,
            Records = [],
        };

        var (status, detail, lastDate, lastOdo) = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: 52000,
            currentDate: new LocalDate(2026, 3, 23));

        status.ShouldBe("dueSoon");
        detail.ShouldContain("No service history", Case.Insensitive);
        lastDate.ShouldBeNull();
        lastOdo.ShouldBeNull();
    }

    [Fact]
    public void Calculate_CombinedInterval_WorseStatusWins_WhenTimeOverdueButMileageOk()
    {
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Brake Fluid",
            MileageInterval = 50_000,
            MileageWarning = 500,
            TimeIntervalMonths = 6,
            TimeWarningDays = 14,
            Records =
            [
                new MaintenanceRecordEntry
                {
                    RecordId = Guid.NewGuid(),
                    ItemId = Guid.NewGuid(),
                    Date = new LocalDate(2025, 1, 1),
                    Odometer = 10000,
                    CreatedAt = SystemClock.Instance.GetCurrentInstant(),
                },
            ],
        };

        // Mileage: only +2,000 since service => OK for a 50k interval.
        // Time: > 6 months since 2025-01-01 (by 2026-01-01) => overdue.
        var (status, detail, _, _) = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: 12000,
            currentDate: new LocalDate(2026, 1, 1));

        status.ShouldBe("overdue");
        detail.ShouldContain("Overdue", Case.Insensitive);
    }

    [Fact]
    public void Calculate_AfterDeletingLatestRecord_ShouldRecalculateFromPreviousRecord()
    {
        var itemId = Guid.NewGuid();
        var record1Id = Guid.NewGuid();
        var record2Id = Guid.NewGuid();

        var item = new MaintenanceItemEntry
        {
            ItemId = itemId,
            Name = "Oil Change",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = null,
            TimeWarningDays = 14,
            LastPerformedDate = null,
            LastPerformedOdometer = null,
            Records =
            [
                new MaintenanceRecordEntry
                {
                    RecordId = record2Id,
                    ItemId = itemId,
                    Date = new LocalDate(2026, 3, 1),
                    Odometer = 50000,
                    CreatedAt = SystemClock.Instance.GetCurrentInstant(),
                },
                new MaintenanceRecordEntry
                {
                    RecordId = record1Id,
                    ItemId = itemId,
                    Date = new LocalDate(2026, 2, 1),
                    Odometer = 45000,
                    CreatedAt = SystemClock.Instance.GetCurrentInstant(),
                },
            ],
        };

        var before = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: 54000,
            currentDate: new LocalDate(2026, 3, 23));

        before.Status.ShouldBe("ok");
        before.LastPerformedOdometer.ShouldBe(50000);

        item.Records.RemoveAll(r => r.RecordId == record2Id);

        var after = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: 54000,
            currentDate: new LocalDate(2026, 3, 23));

        after.LastPerformedOdometer.ShouldBe(45000);
        after.Status.ShouldBe("overdue");
    }

    [Fact]
    public void Calculate_AfterDeletingLastRecord_ShouldFallBackToInitialBaseline()
    {
        var itemId = Guid.NewGuid();
        var recordId = Guid.NewGuid();

        var item = new MaintenanceItemEntry
        {
            ItemId = itemId,
            Name = "Coolant",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = null,
            TimeWarningDays = 14,
            LastPerformedDate = new LocalDate(2026, 1, 1),
            LastPerformedOdometer = 40000,
            Records =
            [
                new MaintenanceRecordEntry
                {
                    RecordId = recordId,
                    ItemId = itemId,
                    Date = new LocalDate(2026, 2, 1),
                    Odometer = 43000,
                    CreatedAt = SystemClock.Instance.GetCurrentInstant(),
                },
            ],
        };

        item.Records.RemoveAll(r => r.RecordId == recordId);

        var result = MaintenanceStatusCalculator.Calculate(
            item,
            estimatedCurrentMileage: 44500,
            currentDate: new LocalDate(2026, 3, 23));

        result.LastPerformedOdometer.ShouldBe(40000);
        result.LastPerformedDate.ShouldBe(new LocalDate(2026, 1, 1));
        result.Status.ShouldBe("dueSoon");
    }
}
