using Freight.Domain.Common;

namespace Freight.Domain.Fleet;

public interface ITripRepository : IRepository<Trip>
{
    /// <summary>
    /// The truck's current open trip, if any - a truck idle at office with no shipments
    /// assigned has none. At most one open trip per truck is an invariant enforced by
    /// application logic, not a stored reference on Truck (see Trip's class doc comment).
    /// </summary>
    Task<Trip?> GetOpenTripByTruckIdAsync(Guid truckId, CancellationToken cancellationToken = default);
}
