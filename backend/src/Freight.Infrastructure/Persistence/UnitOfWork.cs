using Freight.Domain.Common;

namespace Freight.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly FreightDbContext _dbContext;

    public UnitOfWork(FreightDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
