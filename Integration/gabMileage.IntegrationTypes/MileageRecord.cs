using System.ComponentModel.DataAnnotations;

namespace gabMileage.IntegrationTypes;

public class MileageRecord
{
    public Guid Id { get; set; }
    public DateTime FilledAt { get; set; }
    public double StartingMileage { get; set; }
    public double EndingMileage { get; set; }
    public double Gallons { get; set; }
    public decimal Cost { get; set; }
    public string Station { get; set; }
}
