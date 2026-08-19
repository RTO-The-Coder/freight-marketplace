using Freight.Domain.Common;

namespace Freight.Domain.Fleet;

public interface IDriverRepository : IRepository<Driver>
{
    Task<IReadOnlyList<Driver>> GetAllAsync(CancellationToken cancellationToken = default);
}
