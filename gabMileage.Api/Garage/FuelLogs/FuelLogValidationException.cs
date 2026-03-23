using System.Collections.ObjectModel;

namespace gabMileage.Api.Garage.FuelLogs;

public sealed class FuelLogValidationException : Exception
{
    public FuelLogValidationException(IDictionary<string, string[]> errors)
        : base("Fuel log validation failed")
    {
        Errors = new ReadOnlyDictionary<string, string[]>(errors);
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
