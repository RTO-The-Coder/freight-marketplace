using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class TruckingCompanyRepository(FreightDbContext dbContext)
    : Repository<TruckingCompany>(dbContext), ITruckingCompanyRepository
{
    public async Task<IReadOnlyList<TruckingCompany>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<TruckingCompany>().ToListAsync(cancellationToken);
}
