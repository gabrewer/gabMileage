using System.Collections.ObjectModel;

namespace gabMileage.Api.Garage.Vehicles;

public sealed class VehicleValidationException : Exception
{
    public VehicleValidationException(IDictionary<string, string[]> errors)
        : base("Vehicle validation failed")
    {
        Errors = new ReadOnlyDictionary<string, string[]>(errors);
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
