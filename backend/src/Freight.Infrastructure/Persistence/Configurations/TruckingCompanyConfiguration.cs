using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class TruckingCompanyConfiguration : IEntityTypeConfiguration<TruckingCompany>
{
    public void Configure(EntityTypeBuilder<TruckingCompany> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired();

        builder.OwnsOne(c => c.OfficeLocation, loc =>
        {
            loc.Property(l => l.Latitude).HasColumnName("OfficeLatitude");
            loc.Property(l => l.Longitude).HasColumnName("OfficeLongitude");
        });

        builder.Metadata.FindNavigation(nameof(TruckingCompany.Trucks))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(c => c.Trucks, truckBuilder =>
        {
            truckBuilder.HasKey(t => t.Id);
            truckBuilder.WithOwner().HasForeignKey(t => t.TruckingCompanyId);

            truckBuilder.Property(t => t.TruckType);
            truckBuilder.Property(t => t.HazmatCertified);
            truckBuilder.Property(t => t.MovementState)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            truckBuilder.HasOne(t => t.DriverAssignment)
                .WithOne()
                .HasForeignKey<Truck>("DriverAssignmentId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            truckBuilder.OwnsOne(t => t.Capacity, cap =>
            {
                cap.OwnsOne(c => c.Total, total =>
                {
                    total.Property(x => x.WeightKg).HasColumnName("TotalWeightKg");
                    total.Property(x => x.VolumeCubicMeters).HasColumnName("TotalVolumeCubicMeters");
                });
                cap.OwnsOne(c => c.Remaining, remaining =>
                {
                    remaining.Property(x => x.WeightKg).HasColumnName("RemainingWeightKg");
                    remaining.Property(x => x.VolumeCubicMeters).HasColumnName("RemainingVolumeCubicMeters");
                });
            });

            truckBuilder.OwnsMany(t => t.RouteStops, stopBuilder =>
            {
                stopBuilder.Property(s => s.ShipmentId);
                stopBuilder.Property(s => s.Kind);
                stopBuilder.Property(s => s.ExpectedArrivalTime);
                stopBuilder.WithOwner().HasForeignKey("TruckId");
                // "Ordinal" is EF Core's own default shadow surrogate key for a
                // relationally-mapped owned collection (Stop has no ToJson()) - per
                // EF's documented default, this is a database-generated unique value,
                // NOT a per-truck 0,1,2,... list-index (that auto-derivation only
                // applies to JSON-column-mapped owned collections). Values are
                // globally unique/increasing but still assigned in insertion order,
                // which is all Truck.RouteStops' ordering actually depends on -
                // nothing reads Ordinal as a 0-based position. Left database-generated
                // deliberately (not ValueGeneratedNever): that produced every Stop
                // defaulting to the CLR default (0), colliding within the same Truck,
                // since Stop carries no ordinal field of its own for EF to read from.
                stopBuilder.Property<int>("Ordinal");
                stopBuilder.HasKey("TruckId", "Ordinal");
            });
        });
    }
}
