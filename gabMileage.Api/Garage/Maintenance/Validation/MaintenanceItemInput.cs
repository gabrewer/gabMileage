namespace gabMileage.Api.Garage.Maintenance.Validation;

public static class MaintenanceItemInput
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Centralize Maintenance item validation/normalization.
    // Why: Keeps handlers small and ensures 0-from-JSON defaults are applied consistently.
    // Alternatives considered: Apply defaults only in endpoints (rejected: handlers can be invoked directly by Wolverine).
    // Confidence: high

    public static (string Name, int MileageWarning, int TimeWarningDays) NormalizeAndValidate(
        string name,
        int mileageWarning,
        int timeWarningDays)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Maintenance item name is required.", nameof(name));

        var normalizedName = name.Trim();

        // System.Text.Json deserializes omitted non-nullable ints as 0.
        // Treat 0/negative as "use default".
        var normalizedMileageWarning = mileageWarning <= 0
            ? MileageWarningThreshold.Default
            : mileageWarning;

        var normalizedTimeWarningDays = timeWarningDays <= 0
            ? TimeWarningThreshold.DefaultDays
            : timeWarningDays;

        return (normalizedName, normalizedMileageWarning, normalizedTimeWarningDays);
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
