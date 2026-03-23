namespace gabMileage.Api.Garage.Maintenance;

public static class MaintenanceTabDataId
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Define read model key generator for the Maintenance tab.
    // Why: Unit acceptance test + Domain Decision D-04: key is "maintenance-{vehicleId}".
    // Alternatives considered: Use vehicle stream id directly ("vehicle-{id}") (rejected: tab read model needs its own namespace/prefix).
    // Confidence: high
    public static string From(Guid vehicleId) => $"maintenance-{vehicleId:D}";
}
