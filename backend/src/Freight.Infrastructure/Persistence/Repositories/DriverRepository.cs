using Freight.Domain.Fleet;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class DriverRepository(FreightDbContext dbContext)
    : Repository<Driver>(dbContext), IDriverRepository
{
}
