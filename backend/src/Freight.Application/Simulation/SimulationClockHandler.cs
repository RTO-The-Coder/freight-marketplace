using Freight.Domain.Common;
using Freight.Domain.Simulation;

namespace Freight.Application.Simulation;

public sealed record SimulationTimeResponse(DateTime CurrentTime);

public sealed record SetSimulationTimeRequest(DateTime NewCurrentTime);

public sealed record AdvanceSimulationTimeRequest(int Minutes);

/// <summary>
/// The single global <see cref="SimulationClock"/>: read it, overwrite it, or advance
/// it. The clock row is created (seeded at the current real UTC time) the first time any
/// of these is called and no row exists yet - so a fresh database is usable without a
/// separate setup step.
/// </summary>
public sealed class SimulationClockHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<SimulationTimeResponse> GetTimeAsync(CancellationToken cancellationToken = default)
    {
        var clock = await GetOrCreateAsync(cancellationToken);
        return new SimulationTimeResponse(clock.CurrentTime);
    }

    /// <summary>Overwrites simulated time to an explicit value - may move it backward (e.g. resetting a demo run).</summary>
    public async Task<SimulationTimeResponse> SetTimeAsync(SetSimulationTimeRequest request, CancellationToken cancellationToken = default)
    {
        var clock = await GetOrCreateAsync(cancellationToken);
        clock.SetTo(request.NewCurrentTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SimulationTimeResponse(clock.CurrentTime);
    }

    /// <summary>Moves simulated time strictly forward by <see cref="AdvanceSimulationTimeRequest.Minutes"/>.</summary>
    public async Task<SimulationTimeResponse> AdvanceAsync(AdvanceSimulationTimeRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Minutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Minutes, "Cannot advance simulated time by a negative amount.");
        }

        var clock = await GetOrCreateAsync(cancellationToken);
        clock.AdvanceBy(TimeSpan.FromMinutes(request.Minutes));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SimulationTimeResponse(clock.CurrentTime);
    }

    private Task<SimulationClock> GetOrCreateAsync(CancellationToken cancellationToken) =>
        unitOfWork.SimulationClock.GetOrCreateAsync(() => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
}
