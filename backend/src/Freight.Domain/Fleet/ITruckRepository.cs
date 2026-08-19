using Freight.Domain.Common;

namespace Freight.Domain.Fleet;

public interface ITruckRepository : IRepository<Truck>
{
    Task<IReadOnlyList<Truck>> GetByTruckingCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Truck>> GetAllAsync(CancellationToken cancellationToken = default);
}
