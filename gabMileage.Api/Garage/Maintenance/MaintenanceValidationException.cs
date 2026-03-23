using System.Collections.ObjectModel;

namespace gabMileage.Api.Garage.Maintenance;

// Breadcrumbs (backend-builder)
// Who: backend-builder
// What: Dedicated validation exception for maintenance write endpoints.
// Why: Sprint 04 contract requires brief, field-keyed validation errors returned via Results.ValidationProblem.
// Alternatives considered: ArgumentException/ArgumentOutOfRangeException (rejected: no field map, poor client UX).
// Confidence: high
public sealed class MaintenanceValidationException : Exception
{
    public MaintenanceValidationException(IDictionary<string, string[]> errors)
        : base("Maintenance validation failed")
    {
        Errors = new ReadOnlyDictionary<string, string[]>(errors);
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
