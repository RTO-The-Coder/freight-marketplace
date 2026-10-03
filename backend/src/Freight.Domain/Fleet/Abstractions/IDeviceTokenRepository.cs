using Freight.Domain.Common;

namespace Freight.Domain.Fleet.Abstractions;

public interface IDeviceTokenRepository : IRepository<DeviceToken>
{
    Task<DeviceToken?> GetByTruckingCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default);

    /// <summary>All companies' registered devices - used to notify every company on booking (ADR 0007).</summary>
    Task<IReadOnlyList<DeviceToken>> GetAllAsync(CancellationToken cancellationToken = default);
}
