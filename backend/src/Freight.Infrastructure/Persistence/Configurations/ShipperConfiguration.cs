using Freight.Domain.Shipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class ShipperConfiguration : IEntityTypeConfiguration<Shipper>
{
    public void Configure(EntityTypeBuilder<Shipper> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired();
        builder.Property(s => s.ContactEmail).IsRequired();
    }
}
