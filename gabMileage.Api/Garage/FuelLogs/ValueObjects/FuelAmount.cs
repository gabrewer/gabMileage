namespace gabMileage.Api.Garage.FuelLogs.ValueObjects;

public readonly record struct FuelAmount
{
    public const decimal Max = 999.999m;
    public const int MaxDecimalPlaces = 3;

    public decimal Value { get; }

    public FuelAmount(decimal value)
    {
        if (value <= 0m || value > Max)
            throw new ArgumentOutOfRangeException(nameof(value), "Enter a valid amount");

        if (DecimalPlaces(value) > MaxDecimalPlaces)
            throw new ArgumentOutOfRangeException(nameof(value), "Enter a valid amount");

        Value = value;
    }

    private static int DecimalPlaces(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0x7F;
    }

    public override string ToString() => Value.ToString();
}
