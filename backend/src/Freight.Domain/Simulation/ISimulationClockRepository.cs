namespace Freight.Domain.Simulation;

/// <summary>
/// Purpose-built accessor for the single, global <see cref="SimulationClock"/> row - not
/// an <see cref="Common.IRepository{T}"/>, since Remove makes no sense for something that
/// must always have exactly one row, and there's no meaningful id to query by (it's a
/// singleton, not a collection).
/// </summary>
public interface ISimulationClockRepository
{
    /// <summary>The single clock row. Throws if it hasn't been created yet - see <see cref="Add"/>.</summary>
    Task<SimulationClock> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Registers the single clock row for insertion. Only ever called once, at first setup.</summary>
    void Add(SimulationClock clock);
}
