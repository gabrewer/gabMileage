using gabMileage.Api.Garage.Maintenance;
using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.Validation;

public static class MaintenanceRecordInput
{
    public static void Validate(LocalDate date, int odometer, string? notes, IClock clock)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var today = clock.GetCurrentInstant().InUtc().Date;
        if (date > today)
            errors["date"] = ["Date can't be in the future"];

        if (odometer is <= 0 or > 9_999_999)
            errors["odometer"] = ["Enter a valid odometer reading"];

        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (notes is { Length: > 500 })
            errors["notes"] = ["Notes are too long"];

        if (errors.Count > 0)
            throw new MaintenanceValidationException(errors);
    }
}
