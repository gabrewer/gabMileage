using NodaTime;

namespace gabMileage.Api.Garage.Vehicles;

internal static class VehicleInput
{
    private const int NameMaxLength = 50;
    private const int MakeMaxLength = 50;
    private const int ModelMaxLength = 50;
    private const int MinYear = 1900;

    public static (string Name, string? Make, string? Model, int? Year) NormalizeAndValidate(
        string name,
        string? make,
        string? model,
        int? year,
        IClock clock)
    {
        name = (name ?? string.Empty).Trim();
        make = string.IsNullOrWhiteSpace(make) ? null : make.Trim();
        model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(name))
            errors["name"] = ["Name is required."];
        else if (name.Length > NameMaxLength)
            errors["name"] = [$"Name must be {NameMaxLength} characters or fewer."];

        if (make is not null && make.Length > MakeMaxLength)
            errors["make"] = [$"Make must be {MakeMaxLength} characters or fewer."];

        if (model is not null && model.Length > ModelMaxLength)
            errors["model"] = [$"Model must be {ModelMaxLength} characters or fewer."];

        if (year is not null)
        {
            var maxYear = clock.GetCurrentInstant().InZone(DateTimeZone.Utc).Year + 1;
            if (year < MinYear || year > maxYear)
                errors["year"] = [$"Year must be between {MinYear} and {maxYear}."];
        }

        if (errors.Count > 0)
            throw new VehicleValidationException(errors);

        return (name, make, model, year);
    }
}
