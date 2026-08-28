using Freight.Domain.Simulation;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Persistence.Repositories;

public sealed class SimulationClockRepository(FreightDbContext dbContext) : ISimulationClockRepository
{
    public async Task<SimulationClock> GetAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Set<SimulationClock>().SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "The simulation clock has not been set up yet - call SetSimulationClock first.");

    public async Task<SimulationClock> GetOrCreateAsync(Func<DateTime> seedStartingAt, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Set<SimulationClock>().SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = SimulationClock.Create(seedStartingAt());
        dbContext.Set<SimulationClock>().Add(created);
        return created;
    }

    public void Add(SimulationClock clock) => dbContext.Set<SimulationClock>().Add(clock);
}
