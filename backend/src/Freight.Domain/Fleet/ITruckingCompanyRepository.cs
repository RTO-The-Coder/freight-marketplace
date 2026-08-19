using Freight.Domain.Common;

namespace Freight.Domain.Fleet;

public interface ITruckingCompanyRepository : IRepository<TruckingCompany>
{
    Task<IReadOnlyList<TruckingCompany>> GetAllAsync(CancellationToken cancellationToken = default);
}
