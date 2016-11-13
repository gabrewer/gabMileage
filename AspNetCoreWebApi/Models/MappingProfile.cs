using gabMileage.WebApi.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;

namespace gabMileage.WebApi.Models
{
    public class MappingProfile: Profile
    {
        public MappingProfile()
        {
            CreateMap<Mileage, MileageRecord>().ReverseMap();

        }
    }
}
