using Freight.Domain.Shipment;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class ShipmentRepository(FreightDbContext dbContext)
    : Repository<Shipment>(dbContext), IShipmentRepository
{
    public async Task<IReadOnlyList<Shipment>> GetByShipperIdAsync(Guid shipperId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Shipment>()
            .Where(shipment => shipment.ShipperId == shipperId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Shipment>> GetByStatusAsync(ShipmentStatus status, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Shipment>()
            .Where(shipment => shipment.Status == status)
            .ToListAsync(cancellationToken);
}
