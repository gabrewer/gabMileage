namespace gabMileage.Api.Garage.FuelLogs.ValueObjects;

public readonly record struct Odometer
{
    public const int Max = 9_999_999;

    public int Value { get; }

    public Odometer(int value)
    {
        if (value is <= 0 or > Max)
            throw new ArgumentOutOfRangeException(nameof(value), "Enter a valid odometer reading");

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
