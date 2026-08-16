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

    public DbSet<TruckingCompany> TruckingCompanies => Set<TruckingCompany>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverRulePreference> DriverRulePreferences => Set<DriverRulePreference>();
    public DbSet<Freight.Domain.Shipment.Shipper> Shippers => Set<Freight.Domain.Shipment.Shipper>();
    public DbSet<ShipmentAggregate> Shipments => Set<ShipmentAggregate>();
    public DbSet<DriverComplianceState> DriverComplianceStates => Set<DriverComplianceState>();
    public DbSet<RouteProgress> RouteProgresses => Set<RouteProgress>();
    public DbSet<RouteLeg> RouteLegs => Set<RouteLeg>();

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
