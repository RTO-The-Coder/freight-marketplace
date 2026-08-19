using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class DriverRepository(FreightDbContext dbContext)
    : Repository<Driver>(dbContext), IDriverRepository
{
    public async Task<IReadOnlyList<Driver>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<Driver>().ToListAsync(cancellationToken);
}
