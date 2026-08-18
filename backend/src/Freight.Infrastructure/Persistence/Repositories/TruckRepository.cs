using Freight.Domain.Fleet;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class TruckRepository(FreightDbContext dbContext)
    : Repository<Truck>(dbContext), ITruckRepository
{
}
