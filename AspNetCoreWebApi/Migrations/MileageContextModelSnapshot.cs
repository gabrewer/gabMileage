using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using gabMileage.WebApi.Data;

namespace gabMileage.WebApi.Migrations
{
    [DbContext(typeof(MileageContext))]
    partial class MileageContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.0.1")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("gabMileage.WebApi.Data.MileageRecord", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<decimal>("Cost");

                    b.Property<double>("EndingMileage");

                    b.Property<DateTime>("FilledAt");

                    b.Property<double>("Gallons");

                    b.Property<double>("StartingMileage");

                    b.Property<string>("Station");

                    b.Property<Guid>("User");

                    b.HasKey("Id");

                    b.ToTable("MileageRecords");
                });
        }
    }
}
