using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class DeviceTokenRepository(FreightDbContext dbContext)
    : Repository<DeviceToken>(dbContext), IDeviceTokenRepository
{
    public async Task<DeviceToken?> GetByTruckingCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<DeviceToken>()
            .SingleOrDefaultAsync(deviceToken => deviceToken.TruckingCompanyId == truckingCompanyId, cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<DeviceToken>().ToListAsync(cancellationToken);
}
