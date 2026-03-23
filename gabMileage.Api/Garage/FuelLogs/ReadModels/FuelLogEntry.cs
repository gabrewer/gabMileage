using NodaTime;

namespace gabMileage.Api.Garage.FuelLogs.ReadModels;

public sealed class FuelLogEntry
{
    public Guid FuelLogId { get; set; }
    public LocalDate Date { get; set; }
    public int Odometer { get; set; }
    public decimal Gallons { get; set; }
    public decimal TotalCost { get; set; }
    public decimal PricePerGallon { get; set; }
    public string? CalculatedField { get; set; }
    public bool IsPartialFillUp { get; set; }
    public string? Notes { get; set; }
    public decimal? Mpg { get; set; }
    public Instant CreatedAt { get; set; }
}
