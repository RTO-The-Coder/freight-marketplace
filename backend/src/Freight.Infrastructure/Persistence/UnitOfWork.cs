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
    }

    public ITruckingCompanyRepository TruckingCompanies { get; }

    public IShipperRepository Shippers { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
