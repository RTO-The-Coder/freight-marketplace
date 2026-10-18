using Freight.Domain.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class ShipperConfiguration : IEntityTypeConfiguration<Shipper>
{
    public void Configure(EntityTypeBuilder<Shipper> builder)
    {
        builder.ToTable("Shippers");

        builder.HasKey(shipper => shipper.Id);

        builder.Property(shipper => shipper.Name)
            .IsRequired();

        builder.Property(shipper => shipper.ContactEmail)
            .IsRequired();

        // The address book is owned by the Shipper and only reached through it (ADR 0013) -
        // add-only, so rows are never updated or deleted.
        builder.OwnsMany(shipper => shipper.Addresses, address =>
        {
            address.ToTable("ShipperAddresses");

            address.WithOwner().HasForeignKey("ShipperId");

            address.HasKey(a => a.Id);

            // Client-generated in ShipperAddress.Create - same reason as Stop.Id in TripConfiguration:
            // without this EF reads a new non-default Guid key as an existing row and emits an UPDATE.
            address.Property(a => a.Id).ValueGeneratedNever();

            address.Property(a => a.Label)
                .HasMaxLength(ShipperAddress.MaxLabelLength)
                .IsRequired();

            address.Property(a => a.AreaName)
                .HasMaxLength(ShipperAddress.MaxAreaNameLength)
                .IsRequired();

            address.Property(a => a.CreatedAt).IsRequired();

            address.Ignore(a => a.PlaceName);

            address.OwnsOne(a => a.Location, location =>
            {
                location.Property(l => l.Latitude).HasColumnName("Latitude");
                location.Property(l => l.Longitude).HasColumnName("Longitude");
            });

            address.Navigation(a => a.Location).IsRequired();

            address.HasIndex("ShipperId");
        });

        builder.Metadata
            .FindNavigation(nameof(Shipper.Addresses))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
