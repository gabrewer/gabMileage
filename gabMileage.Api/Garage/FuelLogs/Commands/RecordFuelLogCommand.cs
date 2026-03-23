using NodaTime;

namespace gabMileage.Api.Garage.FuelLogs.Commands;

// Breadcrumbs (backend-builder)
// What: Write-side command for recording a new fuel log fill-up.
// Why: Command is the stable handler contract (Wolverine/Marten) and includes client-generated CommandId for idempotency.
// Alternatives considered: Endpoint DTO-only (rejected: Sprint docs require explicit command types).
// Confidence: high
public sealed record RecordFuelLogCommand(
    Guid CommandId,
    Guid VehicleId,
    LocalDate Date,
    int Odometer,
    decimal? Gallons,
    decimal? TotalCost,
    decimal? PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes
);
