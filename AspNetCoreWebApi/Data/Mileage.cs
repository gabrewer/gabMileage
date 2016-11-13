using System;
using System.ComponentModel.DataAnnotations;

namespace gabMileage.WebApi.Data
{
    public class MileageRecord
    {
        public MileageRecord()
        {
        }

        public int Id { get; set; }

        [Required]
        public Guid User { get; set; }

        [Required]
        public DateTime FilledAt { get; set; }

        [Required]
        public double StartingMileage { get; set; }

        [Required]
        public double EndingMileage { get; set; }

        [Required]
        public double Gallons { get; set; }

        public decimal Cost { get; set; }

        public string Station { get; set; }
    }
}
