using Freight.Domain.Shipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");

        builder.HasKey(shipment => shipment.Id);

        builder.Property(shipment => shipment.ShipperId)
            .IsRequired();

        builder.Property(shipment => shipment.TruckingCompanyId);

        builder.Property(shipment => shipment.RequiredTruckType)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(shipment => shipment.OfferDeadline)
            .IsRequired();

        builder.Property(shipment => shipment.ActualPickupAt);

        builder.Property(shipment => shipment.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.OwnsOne(shipment => shipment.PickupLocation, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("PickupLatitude");
            location.Property(l => l.Longitude).HasColumnName("PickupLongitude");
        });

        builder.OwnsOne(shipment => shipment.DeliveryLocation, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("DeliveryLatitude");
            location.Property(l => l.Longitude).HasColumnName("DeliveryLongitude");
        });

        builder.OwnsOne(shipment => shipment.Load, load =>
        {
            load.Property(l => l.WeightKg).HasColumnName("LoadWeightKg");
            load.Property(l => l.VolumeCubicMeters).HasColumnName("LoadVolumeCubicMeters");
        });

        builder.OwnsOne(shipment => shipment.PickupWindow, window =>
        {
            window.Property(w => w.Earliest).HasColumnName("PickupWindowEarliest");
            window.Property(w => w.Latest).HasColumnName("PickupWindowLatest");
        });

        builder.OwnsOne(shipment => shipment.DeliveryWindow, window =>
        {
            window.Property(w => w.Earliest).HasColumnName("DeliveryWindowEarliest");
            window.Property(w => w.Latest).HasColumnName("DeliveryWindowLatest");
        });

        builder.OwnsOne(shipment => shipment.ScheduledPickupWindow, window =>
        {
            window.Property(w => w.Earliest).HasColumnName("ScheduledPickupWindowEarliest");
            window.Property(w => w.Latest).HasColumnName("ScheduledPickupWindowLatest");
        });

        builder.OwnsOne(shipment => shipment.ScheduledDeliveryWindow, window =>
        {
            window.Property(w => w.Earliest).HasColumnName("ScheduledDeliveryWindowEarliest");
            window.Property(w => w.Latest).HasColumnName("ScheduledDeliveryWindowLatest");
        });

        builder.Navigation(shipment => shipment.PickupLocation).IsRequired();
        builder.Navigation(shipment => shipment.DeliveryLocation).IsRequired();
        builder.Navigation(shipment => shipment.Load).IsRequired();
        builder.Navigation(shipment => shipment.PickupWindow).IsRequired();
        builder.Navigation(shipment => shipment.DeliveryWindow).IsRequired();

        builder.HasIndex(shipment => shipment.ShipperId);
        builder.HasIndex(shipment => shipment.TruckingCompanyId);
    }
}
