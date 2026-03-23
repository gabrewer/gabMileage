using gabMileage.Api.Garage.Maintenance;
using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.Validation;

public static class MaintenanceItemInput
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Centralize Sprint 04 Maintenance item validation/normalization.
    // Why: Endpoints must return brief, field-keyed validation messages via Results.ValidationProblem.
    // Alternatives considered: Ad-hoc checks in each handler (rejected: duplication + inconsistent errors).
    // Confidence: high

    public static (string Name, int MileageWarning, int TimeWarningDays) NormalizeAndValidate(
        string name,
        int? mileageInterval,
        int? timeIntervalMonths,
        int mileageWarning,
        int timeWarningDays,
        LocalDate? initialLastPerformedDate,
        int? initialLastPerformedOdometer,
        IClock clock)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 100)
            errors["name"] = ["Give this item a name"];

        if (!mileageInterval.HasValue && !timeIntervalMonths.HasValue)
            errors["intervals"] = ["Set a mileage interval, time interval, or both"];

        if (mileageInterval.HasValue)
        {
            if (mileageInterval.Value is <= 0 or > 999_999)
                errors["mileageInterval"] = ["Enter a valid mileage interval"];
        }

        if (timeIntervalMonths.HasValue)
        {
            if (timeIntervalMonths.Value is <= 0 or > 120)
                errors["timeIntervalMonths"] = ["Enter a valid time interval (1–120 months)"];
        }

        // System.Text.Json deserializes omitted non-nullable ints as 0.
        // Treat 0/negative as "use default".
        var normalizedMileageWarning = mileageWarning <= 0
            ? MileageWarningThreshold.Default
            : mileageWarning;

        var normalizedTimeWarningDays = timeWarningDays <= 0
            ? TimeWarningThreshold.DefaultDays
            : timeWarningDays;

        if (mileageInterval.HasValue && normalizedMileageWarning >= mileageInterval.Value)
            errors["mileageWarning"] = ["Warning threshold must be less than the interval"];

        if (timeIntervalMonths.HasValue)
        {
            var intervalDays = timeIntervalMonths.Value * 30.44;
            if (normalizedTimeWarningDays >= intervalDays)
                errors["timeWarningDays"] = ["Warning threshold must be less than the interval"];
        }

        if (initialLastPerformedDate.HasValue)
        {
            var today = clock.GetCurrentInstant().InUtc().Date;
            if (initialLastPerformedDate.Value > today)
                errors["initialLastPerformedDate"] = ["Date can't be in the future"];
        }

        if (initialLastPerformedOdometer.HasValue)
        {
            if (initialLastPerformedOdometer.Value is <= 0 or > 9_999_999)
                errors["initialLastPerformedOdometer"] = ["Enter a valid odometer reading"];
        }

        if (errors.Count > 0)
            throw new MaintenanceValidationException(errors);

        return (trimmed, normalizedMileageWarning, normalizedTimeWarningDays);
    }
}

public static class MileageWarningThreshold
{
    public const int Default = 500;
}

public static class TimeWarningThreshold
{
    public const int DefaultDays = 14;
}
