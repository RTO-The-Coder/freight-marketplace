namespace Freight.Domain.Tracking;

/// <summary>
/// Progress along a Truck's current route leg, held directly by Truck (see
/// Truck.CurrentProgress) - genuinely truck-level live state, not trip history, which
/// is why it stays on Truck rather than moving under Trip alongside Stop.
/// <see cref="TotalDistanceKm"/>/<see cref="TotalTimeTick"/> must always equal the
/// nearest still-Pending stop's IncomingLegDistanceKm/IncomingLegTimeTick - this is the
/// leg the truck is currently driving toward.
///
/// When a stop is inserted ahead of the truck while a leg is already in progress (the
/// truck's immediate next stop changes), this instance is not resumed - it is REPLACED:
/// the distance/time already covered on the old leg must be banked onto the owning
/// Trip (see Trip.BankPartialLeg) before a fresh RouteProgress is constructed for the
/// new Live-position-to-new-stop leg, since that partial progress doesn't belong to any
/// single Stop once the old leg's target is no longer the immediate next stop.
///
/// There is no GPS/live-odometer feed in this system - the only thing ever observed is
/// how many ticks the driver actually spent Driving (as opposed to Resting/on a break),
/// so <see cref="CurrentDrivingTimeTick"/> is the one real, stored, incrementally-advanced
/// counter; distance-so-far is derived from it via <see cref="GetProgressFraction"/>,
/// not tracked independently - two independently-set counters could disagree about how
/// far along the leg actually is, a real stored one cannot disagree with itself.
///
/// ROUTE is the reference point for what gets stored here (a dumb tick counter with no
/// opinion about why it advanced); DRIVER is the reference point for deciding how many
/// ticks to advance it by (was the driver actually driving, or resting?) - that
/// decision is out of scope for RouteProgress itself and is made by the caller (the
/// deferred stop-reached/tick-advance handler, which reads the driver's compliance
/// ledger) via <see cref="AdvanceByTicks"/>. RouteProgress never reads Driver state
/// directly - aggregates in this codebase only ever reference each other by id, never
/// read each other's internals.
/// </summary>
public sealed class RouteProgress
{
    public double TotalDistanceKm { get; private set; }

    /// <summary>Total time for this leg, expressed in fixed 5-minute ticks (e.g. 6h30m = 390 minutes = 78 ticks).</summary>
    public int TotalTimeTick { get; private set; }

    /// <summary>
    /// Ticks the driver has actually spent Driving on this leg so far (never Resting/
    /// break ticks) - the one real counter; see the class doc comment for why distance
    /// is derived from this rather than tracked as its own field.
    /// </summary>
    public int CurrentDrivingTimeTick { get; private set; }

    /// <summary>Derived from <see cref="CurrentDrivingTimeTick"/> via <see cref="GetProgressFraction"/> - never stored independently.</summary>
    public double CurrentDistanceKm => TotalDistanceKm * GetProgressFraction();

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
        TotalTimeTick = totalTimeTick;
        CurrentDrivingTimeTick = 0;
    }

    /// <summary>
    /// Advances progress by however many ticks the driver actually spent Driving -
    /// never Resting/break ticks. The caller (deferred handler) is responsible for
    /// excluding non-driving ticks before calling this; RouteProgress trusts whatever
    /// count it's given and has no visibility into why.
    /// </summary>
    public void AdvanceByTicks(int drivingTicks)
    {
        if (drivingTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(drivingTicks), drivingTicks, "Driving ticks cannot be negative.");
        }

        CurrentDrivingTimeTick = Math.Min(CurrentDrivingTimeTick + drivingTicks, TotalTimeTick);
    }

    public bool IsLegComplete() => CurrentDrivingTimeTick >= TotalTimeTick;

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
        TotalTimeTick = totalTimeTick;
        CurrentDrivingTimeTick = 0;
    }

    public double GetProgressFraction() => TotalTimeTick == 0 ? 1.0 : (double)CurrentDrivingTimeTick / TotalTimeTick;
}
