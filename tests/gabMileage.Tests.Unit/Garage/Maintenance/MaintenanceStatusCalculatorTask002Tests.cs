using System.Reflection;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceStatusCalculatorTask002Tests
{
    private static (string Status, string StatusDetail, LocalDate? LastPerformedDate, int? LastPerformedOdometer)
        Calculate(MaintenanceItemEntry item, int? estimatedCurrentMileage, LocalDate currentDate)
    {
        var apiAssembly = typeof(MaintenanceItemEntry).Assembly;
        var calculatorType = apiAssembly.GetType("gabMileage.Api.Garage.Maintenance.MaintenanceStatusCalculator");

        calculatorType.ShouldNotBeNull(
            "TASK-002 requires a MaintenanceStatusCalculator type at 'gabMileage.Api.Garage.Maintenance.MaintenanceStatusCalculator'.");

        var calculate = calculatorType!
            .GetMethod("Calculate", BindingFlags.Public | BindingFlags.Static);

        calculate.ShouldNotBeNull(
            "Expected public static Calculate(MaintenanceItemEntry, int? estimatedCurrentMileage, LocalDate currentDate) to exist.");

        object? result;
        try
        {
            result = calculate!.Invoke(null, [item, estimatedCurrentMileage, currentDate]);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw tie.InnerException;
        }

        result.ShouldNotBeNull("Calculate returned null (expected a tuple)." );

        try
        {
            return ((ValueTuple<string, string, LocalDate?, int?>)result!);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Calculate must return (string Status, string StatusDetail, LocalDate? LastPerformedDate, int? LastPerformedOdometer).", ex);
        }
    }

    [Fact]
    public void Calculate_WhenNeverPerformedAndNoInitialBaseline_ShouldReturnDueSoon_WithNoHistoryDetail_AndNeverOverdue()
    {
        // Acceptance Criteria #1
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Oil Change",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = 6,
            TimeWarningDays = 14,
            LastPerformedDate = null,
            LastPerformedOdometer = null,
            Records = [],
        };

        var (status, detail, lastDate, lastOdometer) = Calculate(
            item,
            estimatedCurrentMileage: 999_999,
            currentDate: new LocalDate(2026, 3, 23));

        status.ShouldBe("dueSoon");
        detail.ShouldBe("No service history — log your first service to start tracking");
        lastDate.ShouldBeNull();
        lastOdometer.ShouldBeNull();
    }

    [Fact]
    public void Calculate_WhenBothIntervalsConfigured_WorseStatusWins_AndDetailComesFromWorseDimension_MileageDueSoonTimeOk()
    {
        // Acceptance Criteria #2 (worse status wins)
        // Miles since service = 4,550 -> Due Soon (interval 5,000, warning 500 => dueSoon threshold 4,500)
        // Time since service is far from due -> OK
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Oil Change",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = 24,
            TimeWarningDays = 14,
            Records =
            [
                new MaintenanceRecordEntry
                {
                    RecordId = Guid.NewGuid(),
                    ItemId = Guid.Empty, // irrelevant for calculator
                    Date = new LocalDate(2026, 1, 1),
                    Odometer = 10_000,
                    CreatedAt = Instant.FromUnixTimeSeconds(0),
                },
            ],
        };

        var (status, detail, lastDate, lastOdometer) = Calculate(
            item,
            estimatedCurrentMileage: 14_550,
            currentDate: new LocalDate(2026, 3, 1));

        status.ShouldBe("dueSoon");
        detail.ShouldBe("Due in ~450 mi");

        lastDate.ShouldBe(new LocalDate(2026, 1, 1));
        lastOdometer.ShouldBe(10_000);
    }

    [Fact]
    public void Calculate_WhenMileageOverdueAndTimeOk_ShouldReturnOverdue_WithOverdueByMilesDetail()
    {
        // Confirms mileage-overdue rendering and combined-dimension selection.
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Oil Change",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = 24,
            TimeWarningDays = 14,
            Records =
            [
                new MaintenanceRecordEntry
                {
                    RecordId = Guid.NewGuid(),
                    ItemId = Guid.Empty,
                    Date = new LocalDate(2026, 1, 1),
                    Odometer = 10_000,
                    CreatedAt = Instant.FromUnixTimeSeconds(0),
                },
            ],
        };

        // milesSince = 5,800 => overdue by 800
        var (status, detail, _, _) = Calculate(
            item,
            estimatedCurrentMileage: 15_800,
            currentDate: new LocalDate(2026, 3, 1));

        status.ShouldBe("overdue");
        detail.ShouldBe("Overdue by ~800 mi");
    }

    [Fact]
    public void Calculate_ShouldChooseBaseline_FromMostRecentRecord_ByDateThenOdometer()
    {
        // Acceptance Criteria #3 (baseline selection, which enables correct recalculation after deletes)
        // Two records on the same date; the higher odometer is the effective baseline.
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Brake Fluid",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = null,
            TimeWarningDays = 14,
            Records =
            [
                new MaintenanceRecordEntry
                {
                    RecordId = Guid.NewGuid(),
                    ItemId = Guid.Empty,
                    Date = new LocalDate(2026, 1, 1),
                    Odometer = 900,
                    CreatedAt = Instant.FromUnixTimeSeconds(0),
                },
                new MaintenanceRecordEntry
                {
                    RecordId = Guid.NewGuid(),
                    ItemId = Guid.Empty,
                    Date = new LocalDate(2026, 1, 1),
                    Odometer = 950,
                    CreatedAt = Instant.FromUnixTimeSeconds(0),
                },
            ],
        };

        var (status, detail, lastDate, lastOdometer) = Calculate(
            item,
            estimatedCurrentMileage: 6000,
            currentDate: new LocalDate(2026, 3, 23));

        lastDate.ShouldBe(new LocalDate(2026, 1, 1));
        lastOdometer.ShouldBe(950);

        // If baseline is 950, milesSince=5050 => overdue by 50.
        status.ShouldBe("overdue");
        detail.ShouldBe("Overdue by ~50 mi");
    }

    [Fact]
    public void Calculate_WhenMostRecentRecordIsDeleted_ShouldRecalculateUsingNextMostRecentRecord()
    {
        // Acceptance Criteria #3
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Coolant",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = null,
            TimeWarningDays = 14,
            Records =
            [
                new MaintenanceRecordEntry
                {
                    RecordId = Guid.NewGuid(),
                    ItemId = Guid.Empty,
                    Date = new LocalDate(2026, 2, 1),
                    Odometer = 20_000,
                    CreatedAt = Instant.FromUnixTimeSeconds(0),
                },
                new MaintenanceRecordEntry
                {
                    RecordId = Guid.NewGuid(),
                    ItemId = Guid.Empty,
                    Date = new LocalDate(2026, 1, 1),
                    Odometer = 15_000,
                    CreatedAt = Instant.FromUnixTimeSeconds(0),
                },
            ],
        };

        // With baseline 20,000 => milesSince=4,000 => OK
        var (status1, _, lastDate1, lastOdometer1) = Calculate(
            item,
            estimatedCurrentMileage: 24_000,
            currentDate: new LocalDate(2026, 3, 23));

        status1.ShouldBe("ok");
        lastDate1.ShouldBe(new LocalDate(2026, 2, 1));
        lastOdometer1.ShouldBe(20_000);

        // Simulate deleting the most recent record: remove it from history.
        item.Records.RemoveAll(r => r.Date == new LocalDate(2026, 2, 1) && r.Odometer == 20_000);

        // Now baseline should fall back to the next most recent record (15,000) => milesSince=9,000 => Overdue
        var (status2, _, lastDate2, lastOdometer2) = Calculate(
            item,
            estimatedCurrentMileage: 24_000,
            currentDate: new LocalDate(2026, 3, 23));

        status2.ShouldBe("overdue");
        lastDate2.ShouldBe(new LocalDate(2026, 1, 1));
        lastOdometer2.ShouldBe(15_000);
    }

    [Fact]
    public void Calculate_WhenNoRecordsRemain_ShouldFallBackToInitialBaseline_WhenPresent()
    {
        // Part of Acceptance Criteria #3 (fallback chain: next record -> initial baseline -> never-performed).
        var item = new MaintenanceItemEntry
        {
            ItemId = Guid.NewGuid(),
            Name = "Cabin Air Filter",
            MileageInterval = 5000,
            MileageWarning = 500,
            TimeIntervalMonths = null,
            TimeWarningDays = 14,
            LastPerformedDate = new LocalDate(2026, 1, 15),
            LastPerformedOdometer = 45_230,
            Records = [],
        };

        var (status, _, lastDate, lastOdometer) = Calculate(
            item,
            estimatedCurrentMileage: 49_000,
            currentDate: new LocalDate(2026, 3, 23));

        status.ShouldBe("ok");
        lastDate.ShouldBe(new LocalDate(2026, 1, 15));
        lastOdometer.ShouldBe(45_230);
    }
}
