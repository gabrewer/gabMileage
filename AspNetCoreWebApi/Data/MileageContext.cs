using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gabMileage.WebApi.Data
{
    public class MileageContext: DbContext
    {
        public MileageContext(DbContextOptions<MileageContext> options)
            : base(options)
        { }

        public DbSet<MileageRecord> MileageRecords { get; set; }
    }
}
