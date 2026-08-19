using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class TruckRepository(FreightDbContext dbContext)
    : Repository<Truck>(dbContext), ITruckRepository
{
    public async Task<IReadOnlyList<Truck>> GetByTruckingCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Truck>()
            .Where(truck => truck.TruckingCompanyId == truckingCompanyId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Truck>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<Truck>().ToListAsync(cancellationToken);
}
