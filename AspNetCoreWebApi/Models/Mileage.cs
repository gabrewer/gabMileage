using NodaTime;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace gabMileage.WebApi.Models
{
    public class Mileage
    {
        public int Id { get; set; }

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
