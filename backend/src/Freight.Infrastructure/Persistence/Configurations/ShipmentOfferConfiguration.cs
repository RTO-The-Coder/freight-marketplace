using Freight.Domain.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class ShipmentOfferConfiguration : IEntityTypeConfiguration<ShipmentOffer>
{
    public void Configure(EntityTypeBuilder<ShipmentOffer> builder)
    {
        builder.ToTable("ShipmentOffers");

        builder.HasKey(offer => offer.Id);

        builder.Property(offer => offer.ShipmentId).IsRequired();
        builder.Property(offer => offer.TruckingCompanyId).IsRequired();
        builder.Property(offer => offer.TruckId).IsRequired();
        builder.Property(offer => offer.PickupInsertIndex).IsRequired();
        builder.Property(offer => offer.DeliveryInsertIndex).IsRequired();
        builder.Property(offer => offer.AddedDistanceKm).IsRequired();

        builder.Property(offer => offer.PriceEur)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(offer => offer.LimitAt);
        builder.Property(offer => offer.CreatedAt).IsRequired();

        builder.Property(offer => offer.Status)
            .HasConversion<string>()
            .IsRequired();

        // Not unique: a truck may offer again once the shipper changes the times (old offers are Rejected).
        builder.HasIndex(offer => new { offer.ShipmentId, offer.TruckId });
        builder.HasIndex(offer => offer.TruckingCompanyId);
    }
}
