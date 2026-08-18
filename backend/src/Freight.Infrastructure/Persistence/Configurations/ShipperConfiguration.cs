using Freight.Domain.Shipment;
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
    }
}
