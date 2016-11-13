using NodaTime;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace gabMileage.AspNetCoreMVC.Features.Mileage.Models
{
    public class MileageFormViewModel
    {
        [DataType(DataType.Date)]
        [Required]
        [Display(Name = "Filled Up On")]
        public DateTime FilledDate { get; set; }

        [DataType(DataType.Time)]
        [Required]
        [Display(Name = "Filled Up At")]
        public DateTime FilledTime { get; set; }

        [Required]
        [Display(Name = "Starting Mileage")]
        public double StartingMileage { get; set; }

        [Required]
        [Display(Name = "Ending Mileage")]
        public double EndingMileage { get; set; }

        [Required]
        [Display(Name = "Gallons")]
        public double Gallons { get; set; }

        [Display(Name = "cost")]
        public decimal Cost { get; set; }

        [Display(Name = "Gas Station")]
        public string Station { get; set; }
    }

    public class MileageViewModel
    {
        public MileageFormViewModel Form { get; set; }
        public List<Mileage> MileageRecords { get; set; }

    }
}
