using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using gabMileage.WebApi.Data;

namespace gabMileage.WebApi.Migrations
{
    [DbContext(typeof(MileageContext))]
    [Migration("20161113031114_InitialMigration")]
    partial class InitialMigration
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
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
