using Freight.Domain.Fleet;
using Freight.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;

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
        // See trucking-marketplace-requirements.md Section 18 and the per-slice
        // design docs under docs/design/ for which slice owns which entity's mapping.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FreightDbContext).Assembly);
    }
}
