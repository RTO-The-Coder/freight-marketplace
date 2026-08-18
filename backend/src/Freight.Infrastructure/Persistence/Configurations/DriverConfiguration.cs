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
    }
}
