using gabMileage.Api.Garage.FuelLogs;
using gabMileage.Api.Garage.FuelLogs.ValueObjects;

namespace gabMileage.Api.Garage.FuelLogs.Validation;

public static class FuelFieldCalculator
{
    private const string AtLeastTwoFuelFieldsError = "Enter at least two fuel fields — we'll calculate the third.";

    public static (decimal Gallons, decimal TotalCost, decimal PricePerGallon, string? CalculatedField)
        NormalizeAndValidate(
            decimal? gallons,
            decimal? totalCost,
            decimal? pricePerGallon,
            string? calculatedField)
    {
        if (calculatedField is not null && !CalculatedFieldKind.IsValid(calculatedField))
            throw new FuelLogValidationException(new Dictionary<string, string[]> { ["calculatedField"] = ["Enter a valid value"] });

        var provided = (gallons.HasValue ? 1 : 0)
                     + (totalCost.HasValue ? 1 : 0)
                     + (pricePerGallon.HasValue ? 1 : 0);

        if (provided < 2)
            throw new FuelLogValidationException(new Dictionary<string, string[]> { ["fuel"] = [AtLeastTwoFuelFieldsError] });

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // Validate individual field constraints (for provided fields)
        if (gallons.HasValue)
        {
            try { _ = new FuelAmount(gallons.Value); }
            catch { errors["gallons"] = ["Enter a valid amount"]; }
        }

        if (totalCost.HasValue)
        {
            try { _ = new FuelCost(totalCost.Value); }
            catch { errors["totalCost"] = ["Enter a valid amount"]; }
        }

        if (pricePerGallon.HasValue)
        {
            try { _ = new PricePerGallon(pricePerGallon.Value); }
            catch { errors["pricePerGallon"] = ["Enter a valid price"]; }
        }

        if (errors.Count > 0)
            throw new FuelLogValidationException(errors);

        // Calculate missing field if exactly 2 provided
        if (provided == 2)
        {
            if (!gallons.HasValue && totalCost.HasValue && pricePerGallon.HasValue)
            {
                gallons = pricePerGallon.Value > 0m
                    ? Math.Round(totalCost.Value / pricePerGallon.Value, 3)
                    : 0m;
                calculatedField = CalculatedFieldKind.Gallons;
            }
            else if (!totalCost.HasValue && gallons.HasValue && pricePerGallon.HasValue)
            {
                totalCost = Math.Round(gallons.Value * pricePerGallon.Value, 2);
                calculatedField = CalculatedFieldKind.TotalCost;
            }
            else if (!pricePerGallon.HasValue && gallons.HasValue && totalCost.HasValue)
            {
                pricePerGallon = gallons.Value > 0m
                    ? Math.Round(totalCost.Value / gallons.Value, 3)
                    : 0m;
                calculatedField = CalculatedFieldKind.PricePerGallon;
            }
        }

        // If inconsistent, recalculate pricePerGallon from gallons + totalCost
        if (provided == 3 && calculatedField is null)
        {
            var expected = gallons!.Value > 0m
                ? Math.Round(totalCost!.Value / gallons!.Value, 3)
                : 0m;

            if (Math.Abs(expected - pricePerGallon!.Value) > 0.01m)
            {
                pricePerGallon = expected;
                calculatedField = CalculatedFieldKind.PricePerGallon;
            }
        }

        // Validate computed field(s) too
        try { _ = new FuelAmount(gallons!.Value); }
        catch { throw new FuelLogValidationException(new Dictionary<string, string[]> { ["gallons"] = ["Enter a valid amount"] }); }

        try { _ = new FuelCost(totalCost!.Value); }
        catch { throw new FuelLogValidationException(new Dictionary<string, string[]> { ["totalCost"] = ["Enter a valid amount"] }); }

        try { _ = new PricePerGallon(pricePerGallon!.Value); }
        catch { throw new FuelLogValidationException(new Dictionary<string, string[]> { ["pricePerGallon"] = ["Enter a valid price"] }); }

        return (gallons.Value, totalCost.Value, pricePerGallon.Value, calculatedField);
    }
}
