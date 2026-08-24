using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class TruckConfiguration : IEntityTypeConfiguration<Truck>
{
    public void Configure(EntityTypeBuilder<Truck> builder)
    {
        builder.ToTable("Trucks");

        builder.HasKey(truck => truck.Id);

        builder.Property(truck => truck.TruckName)
            .IsRequired();

        builder.Property(truck => truck.TruckType)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(truck => truck.TruckSize)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(truck => truck.IsActive)
            .IsRequired();

        builder.Property(truck => truck.HazmatCertified)
            .IsRequired();

        // Status and RemainingCapacity are both derived, never stored - see their
        // doc comments on Truck for what they're computed from.
        builder.Ignore(truck => truck.Status);
        builder.Ignore(truck => truck.RemainingCapacity);

        builder.OwnsOne(truck => truck.Capacity, capacity =>
        {
            capacity.OwnsOne(c => c.Total, total =>
            {
                total.Property(t => t.WeightKg).HasColumnName("TotalCapacityWeightKg");
                total.Property(t => t.VolumeCubicMeters).HasColumnName("TotalCapacityVolumeCubicMeters");
            });

            capacity.Navigation(c => c.Total).IsRequired();
        });

        builder.Navigation(truck => truck.Capacity).IsRequired();

        // DriverAssignment is a truck-owned wrapper around references to the independent
        // Driver aggregate - the two driver ids are stored as FK columns on Trucks, and
        // the Driver rows themselves live in their own table.
        builder.OwnsOne(truck => truck.DriverAssignment, assignment =>
        {
            assignment.Property(a => a.ConfigurationType)
                .HasConversion<string>()
                .HasColumnName("DriverConfigurationType");

            assignment.Property(a => a.ActiveDriverId).HasColumnName("ActiveDriverId");

            assignment.HasOne(a => a.PrimaryDriver)
                .WithMany()
                .HasForeignKey("PrimaryDriverId")
                .OnDelete(DeleteBehavior.Restrict);

            assignment.HasOne(a => a.SecondaryDriver)
                .WithMany()
                .HasForeignKey("SecondaryDriverId")
                .OnDelete(DeleteBehavior.Restrict);

            assignment.Navigation(a => a.PrimaryDriver).AutoInclude();
            assignment.Navigation(a => a.SecondaryDriver).AutoInclude();

            assignment.Ignore(a => a.ActiveDriver);
            assignment.Ignore(a => a.HasDriverAbleToDrive);
        });

        // Stops are owned by the Truck and only ever reached through it - the domain
        // model deliberately gives Stop no repository of its own.
        builder.OwnsMany(truck => truck.Stops, stop =>
        {
            stop.ToTable("TruckRouteStops");

            stop.WithOwner().HasForeignKey("TruckId");

            stop.HasKey(s => s.Id);

            stop.Property(s => s.Kind)
                .HasConversion<string>()
                .IsRequired();

            stop.Property(s => s.Sequence).IsRequired();

            stop.Property(s => s.ExpectedArrivalTime).IsRequired();

            stop.OwnsOne(s => s.Location, location =>
            {
                location.Property(l => l.Latitude).HasColumnName("LocationLatitude");
                location.Property(l => l.Longitude).HasColumnName("LocationLongitude");
            });

            stop.Navigation(s => s.Location).IsRequired();

            stop.OwnsOne(s => s.ShipmentLoad, load =>
            {
                load.Property(l => l.WeightKg).HasColumnName("ShipmentLoadWeightKg");
                load.Property(l => l.VolumeCubicMeters).HasColumnName("ShipmentLoadVolumeCubicMeters");
            });

            stop.HasIndex("TruckId");
        });

        builder.Metadata
            .FindNavigation(nameof(Truck.Stops))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // CurrentProgress is null until the truck starts its first leg.
        builder.OwnsOne(truck => truck.CurrentProgress, progress =>
        {
            progress.ToTable("TruckRouteProgresses");

            progress.WithOwner().HasForeignKey("TruckId");
            progress.HasKey("TruckId");

            progress.Property(p => p.TotalDistanceKm).HasColumnName("CurrentProgress_TotalDistanceKm");
            progress.Property(p => p.CurrentDistanceKm).HasColumnName("CurrentProgress_CurrentDistanceKm");
            progress.Property(p => p.TotalTimeTick).HasColumnName("CurrentProgress_TotalTimeTick");
        });
    }
}
