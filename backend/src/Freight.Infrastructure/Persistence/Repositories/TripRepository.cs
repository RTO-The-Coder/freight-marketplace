using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
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

    public async Task<IReadOnlyList<Trip>> GetOpenTripsByCompanyAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Trip>()
            .Where(trip => trip.TruckingCompanyId == truckingCompanyId && trip.CompletedAt == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetCompletedByCompanyAsync(
        Guid truckingCompanyId, DateTime from, DateTime to, Guid? truckId, CancellationToken cancellationToken = default) =>
        await DbContext.Set<Trip>()
            .Where(trip => trip.TruckingCompanyId == truckingCompanyId
                && trip.CompletedAt != null && trip.CompletedAt >= from && trip.CompletedAt < to
                && (truckId == null || trip.TruckId == truckId))
            .OrderByDescending(trip => trip.CompletedAt)
            .ToListAsync(cancellationToken);
}
