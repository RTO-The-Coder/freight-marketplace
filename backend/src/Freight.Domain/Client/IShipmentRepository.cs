using Freight.Domain.Common;

namespace Freight.Domain.Client;

public interface IShipmentRepository : IRepository<Shipment>
{
    Task<IReadOnlyList<Shipment>> GetByShipperIdAsync(Guid shipperId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shipment>> GetByStatusAsync(ShipmentStatus status, CancellationToken cancellationToken = default);
}
