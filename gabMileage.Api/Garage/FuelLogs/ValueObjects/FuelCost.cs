namespace gabMileage.Api.Garage.FuelLogs.ValueObjects;

public readonly record struct FuelCost
{
    public const decimal Max = 9_999.99m;
    public const int MaxDecimalPlaces = 2;

    public decimal Value { get; }

    public FuelCost(decimal value)
    {
        if (value < 0m || value > Max)
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
