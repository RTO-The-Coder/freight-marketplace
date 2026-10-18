using Freight.Domain.Common;

namespace Freight.Domain.Fleet.Abstractions;

public interface IDriverRepository : IRepository<Driver>
{
    Task<IReadOnlyList<Driver>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Driver>> GetByTruckingCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default);
}
