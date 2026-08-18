using Freight.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public class Repository<T>(FreightDbContext dbContext) : IRepository<T> where T : class
{
    protected readonly FreightDbContext DbContext = dbContext;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        DbContext.Set<T>().FindAsync([id], cancellationToken).AsTask();

    public void Add(T entity) => DbContext.Set<T>().Add(entity);

    public void Remove(T entity) => DbContext.Set<T>().Remove(entity);
}
