namespace Freight.Domain.Simulation;

/// <summary>
/// The single, global simulated "now" for the whole system - not real wall-clock time.
/// Exactly one row ever exists. Every handler that needs "now" (driver eligibility
/// projections, ETA computation, Trip.StartedAt, RouteProgress advancement) reads this
/// instead of real time, so the system never mixes real and simulated clocks.
///
/// Two distinct operations change it, deliberately kept separate (different semantics):
/// <see cref="SetTo"/> overwrites to an explicit starting point (may move time backward,
/// e.g. resetting a demo) and <see cref="AdvanceBy"/> moves it strictly forward by an
/// elapsed duration (production ticker firing every 5 simulated minutes, or a demo
/// "jump forward by N hours" action) - advancing should never be able to move time
/// backward, which is why the two are not the same method.
/// </summary>
public sealed class SimulationClock
{
    public Guid Id { get; private set; }
    public DateTime CurrentTime { get; private set; }

    // EF Core materializer only - see the equivalent comment on TruckingCompany's
    // parameterless constructor.
    private SimulationClock()
    {
    }

    private SimulationClock(Guid id, DateTime currentTime)
    {
        Id = id;
        CurrentTime = currentTime;
    }

    /// <summary>Creates the single clock row, seeded at <paramref name="startingAt"/>. Called once, at first setup.</summary>
    public static SimulationClock Create(DateTime startingAt) => new(Guid.NewGuid(), startingAt);

    /// <summary>Overwrites the current time to an explicit value - may move time backward (e.g. resetting a demo run).</summary>
    public void SetTo(DateTime newCurrentTime) => CurrentTime = newCurrentTime;

    /// <summary>Moves time strictly forward by <paramref name="elapsed"/> - never backward.</summary>
    public void AdvanceBy(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "The simulation clock can only advance forward.");
        }

        CurrentTime += elapsed;
    }
}
