using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens");

        builder.HasKey(deviceToken => deviceToken.Id);

        builder.Property(deviceToken => deviceToken.Fid)
            .IsRequired();

        builder.HasIndex(deviceToken => deviceToken.TruckingCompanyId)
            .IsUnique();
    }
}
