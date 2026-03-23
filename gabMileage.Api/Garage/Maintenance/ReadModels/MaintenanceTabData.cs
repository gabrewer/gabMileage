using NodaTime;

namespace gabMileage.Api.Garage.Maintenance.ReadModels;

public sealed class MaintenanceTabData
{
    public string Id { get; set; } = string.Empty;   // "maintenance-{vehicleId}"
    public Guid VehicleId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public List<MaintenanceItemEntry> Items { get; set; } = [];

    public int? LastOdometer { get; set; }
}

public sealed class MaintenanceItemEntry
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;

    public int? MileageInterval { get; set; }
    public int? TimeIntervalMonths { get; set; }
    public int MileageWarning { get; set; }
    public int TimeWarningDays { get; set; }

    public LocalDate? LastPerformedDate { get; set; }
    public int? LastPerformedOdometer { get; set; }

    public List<MaintenanceRecordEntry> Records { get; set; } = [];
}

public sealed class MaintenanceRecordEntry
{
    public Guid RecordId { get; set; }
    public Guid ItemId { get; set; }

    public LocalDate Date { get; set; }
    public int Odometer { get; set; }
    public string? Notes { get; set; }

    public Instant CreatedAt { get; set; }
}
