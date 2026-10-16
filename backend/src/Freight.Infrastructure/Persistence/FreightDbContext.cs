using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence;

public sealed class FreightDbContext : DbContext
{
    public FreightDbContext(DbContextOptions<FreightDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity configurations are added incrementally, one slice at a time,
        // as each slice claims persistence for its own aggregate(s).
        // Each IEntityTypeConfiguration in this assembly is picked up automatically.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FreightDbContext).Assembly);
    }
}
