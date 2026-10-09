using Freight.Domain.Common;

namespace Freight.Domain.Client.Abstractions;

public interface IShipmentOfferRepository : IRepository<ShipmentOffer>
{
    /// <summary>Every offer on this shipment, whatever its status.</summary>
    Task<IReadOnlyList<ShipmentOffer>> GetByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>Every offer on any of these shipments - one round trip for a shipper's whole list.</summary>
    Task<IReadOnlyList<ShipmentOffer>> GetByShipmentIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken = default);

    /// <summary>Every offer this company has sent, whatever its status.</summary>
    Task<IReadOnlyList<ShipmentOffer>> GetByCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default);
}
