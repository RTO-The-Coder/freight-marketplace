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
                stopBuilder.WithOwner().HasForeignKey("TruckId");
                stopBuilder.Property<int>("Ordinal").ValueGeneratedNever();
                stopBuilder.HasKey("TruckId", "Ordinal");
            });
        });
    }
}
