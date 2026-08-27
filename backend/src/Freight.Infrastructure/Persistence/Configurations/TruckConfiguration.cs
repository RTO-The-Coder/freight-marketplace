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

        builder.Property(truck => truck.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(truck => truck.Size)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(truck => truck.IsActive)
            .IsRequired();

        builder.Property(truck => truck.HazmatCertified)
            .IsRequired();

        // Status is derived, never stored - see its doc comment on Truck for what it's
        // computed from. RemainingCapacity is now a method (it needs the truck's current
        // Trip, which Truck no longer owns directly), not a property EF could try to map
        // anyway.
        builder.Ignore(truck => truck.Status);

        builder.OwnsOne(truck => truck.Capacity, capacity =>
        {
            capacity.Property(c => c.WeightKg).HasColumnName("TotalCapacityWeightKg");
            capacity.Property(c => c.VolumeCubicMeters).HasColumnName("TotalCapacityVolumeCubicMeters");
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

        // Stops now belong to Trip, not Truck directly - see TripConfiguration. A
        // truck's route is reached through its currently-open Trip (queried by TruckId +
        // CompletedAt IS NULL), not through a collection owned here.

        // CurrentProgress is null until the truck starts its first leg. Stays directly on
        // Truck (not moved under Trip alongside Stop) - it's genuinely truck-level live
        // state, not trip history.
        builder.OwnsOne(truck => truck.CurrentProgress, progress =>
        {
            progress.ToTable("TruckRouteProgresses");

            progress.WithOwner().HasForeignKey("TruckId");
            progress.HasKey("TruckId");

            progress.Property(p => p.TotalDistanceKm).HasColumnName("CurrentProgress_TotalDistanceKm");
            progress.Property(p => p.TotalTimeTick).HasColumnName("CurrentProgress_TotalTimeTick");

            // CurrentDistanceKm is derived (TotalDistanceKm * GetProgressFraction()), not
            // stored - CurrentDrivingTimeTick is the one real, persisted counter.
            progress.Ignore(p => p.CurrentDistanceKm);
            progress.Property(p => p.CurrentDrivingTimeTick).HasColumnName("CurrentProgress_CurrentDrivingTimeTick");
        });
    }
}
