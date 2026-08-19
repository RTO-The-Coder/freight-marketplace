using Freight.Domain.Common;

namespace Freight.Domain.Shipment;

public interface IShipperRepository : IRepository<Shipper>
{
    Task<IReadOnlyList<Shipper>> GetAllAsync(CancellationToken cancellationToken = default);
}
