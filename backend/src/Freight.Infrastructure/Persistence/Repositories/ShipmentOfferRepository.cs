using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class ShipmentOfferRepository(FreightDbContext dbContext)
    : Repository<ShipmentOffer>(dbContext), IShipmentOfferRepository
{
    public async Task<IReadOnlyList<ShipmentOffer>> GetByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<ShipmentOffer>()
            .Where(offer => offer.ShipmentId == shipmentId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ShipmentOffer>> GetByShipmentIdsAsync(
        IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken = default) =>
        shipmentIds.Count == 0
            ? []
            : await DbContext.Set<ShipmentOffer>()
                .Where(offer => shipmentIds.Contains(offer.ShipmentId))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ShipmentOffer>> GetByCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<ShipmentOffer>()
            .Where(offer => offer.TruckingCompanyId == truckingCompanyId)
            .ToListAsync(cancellationToken);
}
