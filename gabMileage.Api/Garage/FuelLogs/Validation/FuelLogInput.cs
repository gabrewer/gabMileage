using gabMileage.Api.Garage.FuelLogs;
using NodaTime;

namespace gabMileage.Api.Garage.FuelLogs.Validation;

public static class FuelLogInput
{
    // Acceptance criteria: Future dates are rejected with "Date can't be in the future".
    public static void Validate(LocalDate date, int odometer, string? notes, IClock clock)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // Date: not in the future (use UTC to avoid hardcoding a specific region/timezone)
        var today = clock.GetCurrentInstant()
            .InUtc()
            .Date;

        if (date > today)
            errors["date"] = ["Date can't be in the future"];

        // Odometer: positive, ≤ 9,999,999
        if (odometer is <= 0 or > 9_999_999)
            errors["odometer"] = ["Enter a valid odometer reading"];

        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        // Notes: max 500 chars
        if (notes is { Length: > 500 })
            errors["notes"] = ["Notes are too long"];

        if (errors.Count > 0)
            throw new FuelLogValidationException(errors);
    }
}
