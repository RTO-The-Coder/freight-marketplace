using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Shipment;
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
        Drivers = new DriverRepository(dbContext);
    }

    public ITruckingCompanyRepository TruckingCompanies { get; }

    public IShipperRepository Shippers { get; }

    public ITruckRepository Trucks { get; }

    public IDriverRepository Drivers { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
