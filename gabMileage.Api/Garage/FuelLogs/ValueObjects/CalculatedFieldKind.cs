namespace gabMileage.Api.Garage.FuelLogs.ValueObjects;

public static class CalculatedFieldKind
{
    public const string Gallons = "gallons";
    public const string TotalCost = "totalCost";
    public const string PricePerGallon = "pricePerGallon";

    public static bool IsValid(string value) => value is Gallons or TotalCost or PricePerGallon;
}
