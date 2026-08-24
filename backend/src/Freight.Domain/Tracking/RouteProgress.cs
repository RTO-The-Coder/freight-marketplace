namespace Freight.Domain.Tracking;

/// <summary>
/// Progress along a Truck's current route leg, held directly by Truck (see
/// Truck.CurrentProgress). A single fraction - <see cref="GetProgressFraction"/> -
/// represents both distance-progress and time-progress along the leg (fraction of
/// distance == fraction of time is a deliberate Phase 1 simplification; see the
/// domain model doc's RouteProgress section).
/// </summary>
public sealed class RouteProgress
{
    public double TotalDistanceKm { get; private set; }
    public double CurrentDistanceKm { get; private set; }

    /// <summary>Total time for this leg, expressed in fixed 5-minute ticks (e.g. 6h30m = 390 minutes = 78 ticks).</summary>
    public int TotalTimeTick { get; private set; }

    // EF Core materializer only - see the equivalent comment on Truck's parameterless
    // constructor.
    private RouteProgress()
    {
    }

    public RouteProgress(double totalDistanceKm, int totalTimeTick)
    {
        if (totalDistanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDistanceKm), totalDistanceKm, "Total distance cannot be negative.");
        }

        if (totalTimeTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTimeTick), totalTimeTick, "Total time tick cannot be negative.");
        }

        TotalDistanceKm = totalDistanceKm;
        CurrentDistanceKm = 0;
        TotalTimeTick = totalTimeTick;
    }

    public void UpdateProgress(double currentDistanceKm)
    {
        if (currentDistanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentDistanceKm), currentDistanceKm, "Current distance cannot be negative.");
        }

        CurrentDistanceKm = currentDistanceKm;
    }

    public bool IsLegComplete() => CurrentDistanceKm >= TotalDistanceKm;

    public void StartNewLeg(double totalDistanceKm, int totalTimeTick)
    {
        if (totalDistanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDistanceKm), totalDistanceKm, "Total distance cannot be negative.");
        }

        if (totalTimeTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTimeTick), totalTimeTick, "Total time tick cannot be negative.");
        }

        TotalDistanceKm = totalDistanceKm;
        CurrentDistanceKm = 0;
        TotalTimeTick = totalTimeTick;
    }

    public double GetProgressFraction() => TotalDistanceKm == 0 ? 1.0 : CurrentDistanceKm / TotalDistanceKm;
}
