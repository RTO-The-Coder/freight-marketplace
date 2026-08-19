using Freight.Domain.Common;

namespace Freight.Domain.Shipment;

public interface IShipmentRepository : IRepository<Shipment>
{
    Task<IReadOnlyList<Shipment>> GetByShipperIdAsync(Guid shipperId, CancellationToken cancellationToken = default);
}
