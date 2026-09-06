using Freight.Domain.Common;

namespace Freight.Domain.Client.Abstractions;

public interface IShipperRepository : IRepository<Shipper>
{
    Task<IReadOnlyList<Shipper>> GetAllAsync(CancellationToken cancellationToken = default);
}
