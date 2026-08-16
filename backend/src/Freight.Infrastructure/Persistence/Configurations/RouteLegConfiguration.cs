using Freight.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class RouteLegConfiguration : IEntityTypeConfiguration<RouteLeg>
{
    public void Configure(EntityTypeBuilder<RouteLeg> builder)
    {
        builder.HasKey(l => new { l.TruckId, l.LegIndex });

        builder.Property(l => l.DurationTicks);
    }
}
