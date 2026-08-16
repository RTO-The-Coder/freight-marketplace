using Freight.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class DriverRulePreferenceConfiguration : IEntityTypeConfiguration<DriverRulePreference>
{
    public void Configure(EntityTypeBuilder<DriverRulePreference> builder)
    {
        builder.HasKey(p => p.DriverId);

        builder.Property(p => p.BreakPreference).IsRequired();
        builder.Property(p => p.DailyRestPreference).IsRequired();
        builder.Property(p => p.WeeklyRestPreference).IsRequired();
        builder.Property(p => p.ExtendDailyDrivingWhenEligible).IsRequired();

        builder.HasOne<Freight.Domain.Fleet.Driver>()
            .WithOne()
            .HasForeignKey<DriverRulePreference>(p => p.DriverId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
