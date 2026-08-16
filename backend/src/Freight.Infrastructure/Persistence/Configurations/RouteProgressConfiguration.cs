using Freight.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class RouteProgressConfiguration : IEntityTypeConfiguration<RouteProgress>
{
    public void Configure(EntityTypeBuilder<RouteProgress> builder)
    {
        builder.HasKey(r => r.TruckId);

        builder.Property(r => r.CurrentLegIndex).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(r => r.TicksElapsedInCurrentLeg).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(r => r.DomainEvents);
    }
}
