using System.Globalization;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using NodaTime;

namespace gabMileage.Api.Garage.Maintenance;

public static class MaintenanceStatusCalculator
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Implement query-time maintenance status calculation (OK/DueSoon/Overdue + detail text) as a pure function.
    // Why: Status depends on "today" and latest odometer, so storing it in projections would go stale.
    // Alternatives considered: Persist status fields in MaintenanceTabData (rejected: would require background recalculation).
    // Confidence: high

    private const string Ok = "ok";
    private const string DueSoon = "dueSoon";
    private const string Overdue = "overdue";

    public static (string Status, string StatusDetail, LocalDate? LastPerformedDate, int? LastPerformedOdometer)
        Calculate(
            MaintenanceItemEntry item,
            int? estimatedCurrentMileage,
            LocalDate currentDate)
    {
        ArgumentNullException.ThrowIfNull(item);

        var (lastDate, lastOdometer) = GetEffectiveBaseline(item);

        // Special case: never performed + no initial baseline
        if (lastDate is null && lastOdometer is null)
        {
            return (
                DueSoon,
                "No service history — log your first service to start tracking",
                null,
                null);
        }

        // Edge case: mileage interval set but we can't evaluate mileage (no odometer data anywhere)
        // and there is no time interval to fall back to.
        if (item.MileageInterval.HasValue
            && !item.TimeIntervalMonths.HasValue
            && (lastOdometer is null || estimatedCurrentMileage is null))
        {
            return (
                DueSoon,
                "Log a fill-up or service to start mileage tracking",
                lastDate,
                lastOdometer);
        }

        var mileage = CalculateMileageOutcome(
            item.MileageInterval,
            item.MileageWarning,
            lastOdometer,
            estimatedCurrentMileage);

        var time = CalculateTimeOutcome(
            item.TimeIntervalMonths,
            item.TimeWarningDays,
            lastDate,
            currentDate);

        var combined = Combine(mileage, time);

        var detail = combined.Status switch
        {
            Ok => BuildOkDetail(mileage, time),
            DueSoon => BuildDueSoonDetail(mileage, time),
            Overdue => BuildOverdueDetail(mileage, time),
            _ => throw new InvalidOperationException($"Unknown status '{combined.Status}'.")
        };

        return (combined.Status, detail, lastDate, lastOdometer);
    }

    private static (LocalDate? Date, int? Odometer) GetEffectiveBaseline(MaintenanceItemEntry item)
    {
        var mostRecent = item.Records
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Odometer)
            .FirstOrDefault();

        if (mostRecent is not null)
            return (mostRecent.Date, mostRecent.Odometer);

        // Fall back to the initial baseline seeded at item creation time.
        return (item.LastPerformedDate, item.LastPerformedOdometer);
    }

    private static CombinedStatus Combine(DimensionOutcome mileage, DimensionOutcome time)
    {
        if (!mileage.Applicable && !time.Applicable)
            return new CombinedStatus(Ok);

        if (mileage.Applicable && !time.Applicable)
            return new CombinedStatus(mileage.Status);

        if (!mileage.Applicable && time.Applicable)
            return new CombinedStatus(time.Status);

        // Both applicable: worse status wins.
        if (mileage.Severity > time.Severity)
            return new CombinedStatus(mileage.Status);

        if (time.Severity > mileage.Severity)
            return new CombinedStatus(time.Status);

        // Same severity: keep the shared status (detail can include both dimensions).
        return new CombinedStatus(mileage.Status);
    }

    private static string BuildOkDetail(DimensionOutcome mileage, DimensionOutcome time)
    {
        var parts = new[] { mileage.PartOk, time.PartOk }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        return parts.Length switch
        {
            0 => "Next due",
            1 => $"Next due in {parts[0]}",
            _ => $"Next due in {parts[0]} or {parts[1]}"
        };
    }

    private static string BuildDueSoonDetail(DimensionOutcome mileage, DimensionOutcome time)
    {
        var parts = new[] { mileage.PartDueSoon, time.PartDueSoon }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        // If only one dimension is DueSoon, detail comes from that dimension.
        // If both are DueSoon, include both parts ("whichever comes first").
        return parts.Length switch
        {
            0 => "Due soon",
            1 => $"Due in {parts[0]}",
            _ => $"Due in {parts[0]} or {parts[1]}"
        };
    }

    private static string BuildOverdueDetail(DimensionOutcome mileage, DimensionOutcome time)
    {
        var parts = new[] { mileage.PartOverdue, time.PartOverdue }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        // If only one dimension is Overdue, detail comes from that dimension.
        // If both are Overdue, include both parts.
        return parts.Length switch
        {
            0 => "Overdue",
            1 => $"Overdue by {parts[0]}",
            _ => $"Overdue by {parts[0]} or {parts[1]}"
        };
    }

    private static DimensionOutcome CalculateMileageOutcome(
        int? mileageInterval,
        int mileageWarning,
        int? lastServiceOdometer,
        int? estimatedCurrentMileage)
    {
        if (!mileageInterval.HasValue)
            return DimensionOutcome.NotApplicable;

        if (lastServiceOdometer is null || estimatedCurrentMileage is null)
            return DimensionOutcome.NotApplicable;

        var interval = mileageInterval.Value;
        var milesSinceService = Math.Max(0, estimatedCurrentMileage.Value - lastServiceOdometer.Value);

        if (milesSinceService >= interval)
        {
            var overdueBy = milesSinceService - interval;
            return DimensionOutcome.Overdue(PartMiles(overdueBy));
        }

        if (milesSinceService >= interval - mileageWarning)
        {
            var dueIn = interval - milesSinceService;
            return DimensionOutcome.DueSoon(PartMiles(dueIn));
        }

        var nextDueIn = interval - milesSinceService;
        return DimensionOutcome.Ok(PartMiles(nextDueIn));
    }

    private static DimensionOutcome CalculateTimeOutcome(
        int? timeIntervalMonths,
        int timeWarningDays,
        LocalDate? lastServiceDate,
        LocalDate currentDate)
    {
        if (!timeIntervalMonths.HasValue)
            return DimensionOutcome.NotApplicable;

        if (lastServiceDate is null)
            return DimensionOutcome.NotApplicable;

        var daysSinceService = Math.Max(0, Period.Between(lastServiceDate.Value, currentDate, PeriodUnits.Days).Days);
        var intervalDays = timeIntervalMonths.Value * 30.44;

        if (daysSinceService >= intervalDays)
        {
            var overdueDays = daysSinceService - intervalDays;
            return DimensionOutcome.Overdue(PartTime(overdueDays));
        }

        if (daysSinceService >= intervalDays - timeWarningDays)
        {
            var dueInDays = intervalDays - daysSinceService;
            return DimensionOutcome.DueSoon(PartTime(dueInDays));
        }

        var nextDueInDays = intervalDays - daysSinceService;
        return DimensionOutcome.Ok(PartTime(nextDueInDays));
    }

    private static string PartMiles(int miles)
    {
        var formatted = miles.ToString("N0", CultureInfo.InvariantCulture);
        return $"~{formatted} mi";
    }

    private static string PartTime(double days)
    {
        var roundedDays = Math.Max(0, (int)Math.Round(days, MidpointRounding.AwayFromZero));

        if (roundedDays >= 45)
        {
            var months = Math.Max(1, (int)Math.Round(roundedDays / 30.44, MidpointRounding.AwayFromZero));
            return $"{months} month{(months == 1 ? string.Empty : "s")}";
        }

        if (roundedDays >= 7)
        {
            var weeks = Math.Max(1, (int)Math.Round(roundedDays / 7.0, MidpointRounding.AwayFromZero));
            return $"{weeks} week{(weeks == 1 ? string.Empty : "s")}";
        }

        return $"{roundedDays} day{(roundedDays == 1 ? string.Empty : "s")}";
    }

    private sealed record CombinedStatus(string Status);

    private readonly record struct DimensionOutcome(
        bool Applicable,
        string Status,
        int Severity,
        string? PartOk,
        string? PartDueSoon,
        string? PartOverdue)
    {
        public static DimensionOutcome NotApplicable => new(false, MaintenanceStatusCalculator.Ok, SeverityOk, null, null, null);

        private const int SeverityOk = 0;
        private const int SeverityDueSoon = 1;
        private const int SeverityOverdue = 2;

        public static DimensionOutcome Ok(string partOk) =>
            new(true, MaintenanceStatusCalculator.Ok, SeverityOk, partOk, null, null);

        public static DimensionOutcome DueSoon(string partDueSoon) =>
            new(true, MaintenanceStatusCalculator.DueSoon, SeverityDueSoon, null, partDueSoon, null);

        public static DimensionOutcome Overdue(string partOverdue) =>
            new(true, MaintenanceStatusCalculator.Overdue, SeverityOverdue, null, null, partOverdue);
    }
}
