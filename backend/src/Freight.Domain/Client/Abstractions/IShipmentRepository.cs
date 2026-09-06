using Freight.Domain.Client.Enums;
using Freight.Domain.Common;

namespace Freight.Domain.Client.Abstractions;

public interface IShipmentRepository : IRepository<Shipment>
{
    Task<IReadOnlyList<Shipment>> GetByShipperIdAsync(Guid shipperId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shipment>> GetByStatusAsync(ShipmentStatus status, CancellationToken cancellationToken = default);
}
