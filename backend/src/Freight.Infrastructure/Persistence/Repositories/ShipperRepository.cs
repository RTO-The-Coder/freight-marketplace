using Freight.Domain.Shipment;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class ShipperRepository(FreightDbContext dbContext)
    : Repository<Shipper>(dbContext), IShipperRepository
{
}
