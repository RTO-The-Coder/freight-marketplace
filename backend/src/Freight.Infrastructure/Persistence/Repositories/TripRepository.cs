using Freight.Domain.Fleet;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class TripRepository(FreightDbContext dbContext)
    : Repository<Trip>(dbContext), ITripRepository
{
    public async Task<Trip?> GetOpenTripByTruckIdAsync(Guid truckId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Trip>()
            .SingleOrDefaultAsync(trip => trip.TruckId == truckId && trip.CompletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetOpenTripsAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<Trip>()
            .Where(trip => trip.CompletedAt == null)
            .ToListAsync(cancellationToken);
}
