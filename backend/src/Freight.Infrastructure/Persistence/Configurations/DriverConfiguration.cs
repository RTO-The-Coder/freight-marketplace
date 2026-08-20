using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");

        builder.HasKey(driver => driver.Id);

        builder.Property(driver => driver.FirstName)
            .IsRequired();

        builder.Property(driver => driver.LastName)
            .IsRequired();

        builder.OwnsOne(driver => driver.Rules, rules =>
        {
            rules.Property(r => r.BreakRule)
                .HasConversion<string>()
                .HasColumnName("BreakRule")
                .IsRequired();

            rules.Property(r => r.DailyRestRule)
                .HasConversion<string>()
                .HasColumnName("DailyRestRule")
                .IsRequired();

            rules.Property(r => r.WeeklyRestRule)
                .HasConversion<string>()
                .HasColumnName("WeeklyRestRule")
                .IsRequired();

            rules.Property(r => r.ExtendDailyDrivingWhenEligible)
                .HasColumnName("ExtendDailyDrivingWhenEligible")
                .IsRequired();
        });

        builder.Navigation(driver => driver.Rules).IsRequired();

        // ComplianceState is null until the driver first starts driving (Driver.StartDriving) -
        // an optional 1:1 owned type, not required like Rules.
        builder.OwnsOne(driver => driver.ComplianceState, state =>
        {
            state.WithOwner().HasForeignKey(s => s.DriverId);
            state.HasKey(s => s.DriverId);

            state.Property(s => s.CurrentActivity)
                .HasConversion<string>()
                .HasColumnName("ComplianceState_CurrentActivity");

            state.Property(s => s.MinutesRemainingInCurrentActivity)
                .HasColumnName("ComplianceState_MinutesRemainingInCurrentActivity");

            state.Property(s => s.ContinuousDrivingMinutesSinceBreak)
                .HasColumnName("ComplianceState_ContinuousDrivingMinutesSinceBreak");

            state.Property(s => s.AwaitingSecondBreakBlock)
                .HasColumnName("ComplianceState_AwaitingSecondBreakBlock");

            state.Property(s => s.DailyDrivingMinutesToday)
                .HasColumnName("ComplianceState_DailyDrivingMinutesToday");

            state.Property(s => s.ExtendedDaysUsedThisWeek)
                .HasColumnName("ComplianceState_ExtendedDaysUsedThisWeek");

            state.Property(s => s.IsTodayExtended)
                .HasColumnName("ComplianceState_IsTodayExtended");

            state.Property(s => s.AwaitingSecondDailyRestBlock)
                .HasColumnName("ComplianceState_AwaitingSecondDailyRestBlock");

            state.Property(s => s.ReducedDailyRestsUsedSinceWeeklyRest)
                .HasColumnName("ComplianceState_ReducedDailyRestsUsedSinceWeeklyRest");

            state.Property(s => s.WeeklyDrivingMinutesThisWeek)
                .HasColumnName("ComplianceState_WeeklyDrivingMinutesThisWeek");

            state.Property(s => s.WeeklyDrivingMinutesPriorWeek)
                .HasColumnName("ComplianceState_WeeklyDrivingMinutesPriorWeek");

            state.Property(s => s.LastEvaluatedSimulatedTime)
                .HasColumnName("ComplianceState_LastEvaluatedSimulatedTime");
        });
    }
}
