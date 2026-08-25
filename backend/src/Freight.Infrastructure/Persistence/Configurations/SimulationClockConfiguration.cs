using Freight.Domain.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freight.Infrastructure.Persistence.Configurations;

public sealed class SimulationClockConfiguration : IEntityTypeConfiguration<SimulationClock>
{
    public void Configure(EntityTypeBuilder<SimulationClock> builder)
    {
        builder.ToTable("SimulationClock");

        builder.HasKey(clock => clock.Id);

        builder.Property(clock => clock.CurrentTime).IsRequired();
    }
}
