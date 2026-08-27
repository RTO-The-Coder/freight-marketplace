using Freight.Domain.Client;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class ShipperRepository(FreightDbContext dbContext)
    : Repository<Shipper>(dbContext), IShipperRepository
{
    public async Task<IReadOnlyList<Shipper>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<Shipper>().ToListAsync(cancellationToken);
}
