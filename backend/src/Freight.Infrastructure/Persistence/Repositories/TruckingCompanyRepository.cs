using Freight.Domain.Fleet;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class TruckingCompanyRepository(FreightDbContext dbContext)
    : Repository<TruckingCompany>(dbContext), ITruckingCompanyRepository
{
}
