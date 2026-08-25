using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Shipment;
using Freight.Domain.Simulation;
using Freight.Infrastructure.Persistence.Repositories;

namespace Freight.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly FreightDbContext _dbContext;

    public UnitOfWork(FreightDbContext dbContext)
    {
        _dbContext = dbContext;
        TruckingCompanies = new TruckingCompanyRepository(dbContext);
        Shippers = new ShipperRepository(dbContext);
        Trucks = new TruckRepository(dbContext);
        Trips = new TripRepository(dbContext);
        Drivers = new DriverRepository(dbContext);
        Shipments = new ShipmentRepository(dbContext);
        SimulationClock = new SimulationClockRepository(dbContext);
    }

    public ITruckingCompanyRepository TruckingCompanies { get; }

    public IShipperRepository Shippers { get; }

    public ITruckRepository Trucks { get; }

    public ITripRepository Trips { get; }

    public IDriverRepository Drivers { get; }

    public IShipmentRepository Shipments { get; }

    public ISimulationClockRepository SimulationClock { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
