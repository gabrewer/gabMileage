namespace gabMileage.Api.Garage.FuelLogs.ValueObjects;

public readonly record struct PricePerGallon
{
    public const decimal Max = 99.999m;
    public const int MaxDecimalPlaces = 3;

    public decimal Value { get; }

    public PricePerGallon(decimal value)
    {
        if (value < 0m || value > Max)
            throw new ArgumentOutOfRangeException(nameof(value), "Enter a valid price");

        if (DecimalPlaces(value) > MaxDecimalPlaces)
            throw new ArgumentOutOfRangeException(nameof(value), "Enter a valid price");

        Value = value;
    }

    private static int DecimalPlaces(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0x7F;
    }

    public override string ToString() => Value.ToString();
}
