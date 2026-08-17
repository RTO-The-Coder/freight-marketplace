using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<ShipmentAggregate>
{
    public void Configure(EntityTypeBuilder<ShipmentAggregate> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.ShipperId);
        builder.Property(s => s.CargoKind);
        builder.Property(s => s.PickupWindowStart);
        builder.Property(s => s.PickupWindowEnd);
        builder.Property(s => s.DeliveryDeadline);

        builder.HasOne<Freight.Domain.Shipment.Shipper>()
            .WithMany()
            .HasForeignKey(s => s.ShipperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(s => s.PickupLocation, loc =>
        {
            loc.Property(l => l.Latitude).HasColumnName("PickupLatitude");
            loc.Property(l => l.Longitude).HasColumnName("PickupLongitude");
        });

        builder.OwnsOne(s => s.DeliveryLocation, loc =>
        {
            loc.Property(l => l.Latitude).HasColumnName("DeliveryLatitude");
            loc.Property(l => l.Longitude).HasColumnName("DeliveryLongitude");
        });

        builder.OwnsOne(s => s.CargoSize, cargo =>
        {
            cargo.Property(c => c.WeightKg).HasColumnName("WeightKg");
            cargo.Property(c => c.VolumeCubicMeters).HasColumnName("VolumeCubicMeters");
        });
    }
}
