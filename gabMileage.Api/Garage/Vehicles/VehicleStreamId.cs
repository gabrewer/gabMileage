namespace gabMileage.Api.Garage.Vehicles;

// Breadcrumbs (backend-builder)
// What: Centralize string stream id format for vehicle streams.
// Why: StreamIdentity is configured as string (to support user-id streams), so vehicle streams must be addressable via a stable string key.
// Alternatives considered: Using Guid stream identity globally (rejected: breaks user-id stream usage).
// Confidence: high
public static class VehicleStreamId
{
    private const string Prefix = "vehicle-";

    public static string From(Guid vehicleId) => $"{Prefix}{vehicleId:D}";
}
