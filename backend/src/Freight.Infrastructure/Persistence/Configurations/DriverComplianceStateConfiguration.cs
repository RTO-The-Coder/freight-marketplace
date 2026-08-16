using Freight.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class DriverComplianceStateConfiguration : IEntityTypeConfiguration<DriverComplianceState>
{
    public void Configure(EntityTypeBuilder<DriverComplianceState> builder)
    {
        builder.HasKey(d => d.DriverId);

        builder.Property(d => d.CurrentActivity).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.MinutesRemainingInCurrentActivity).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.ContinuousDrivingMinutesSinceBreak).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.AwaitingSecondBreakBlock).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.DailyDrivingMinutesToday).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.ExtendedDaysUsedThisWeek).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.IsTodayExtended).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.AwaitingSecondDailyRestBlock).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.ReducedDailyRestsUsedSinceWeeklyRest).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.WeeklyDrivingMinutesThisWeek).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.WeeklyDrivingMinutesPriorWeek).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(d => d.LastEvaluatedSimulatedTime).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Freight.Domain.Fleet.Driver>()
            .WithOne()
            .HasForeignKey<DriverComplianceState>(d => d.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(d => d.DomainEvents);
    }
}
