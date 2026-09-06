using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// A waypoint on a <see cref="Trip"/>'s route, owned by the trip (no repository of its
/// own). <see cref="ShipmentId"/> is set for Pickup/Delivery stops,
/// <see cref="TruckingCompanyId"/> only for the Office stop. Never deleted - a reached
/// stop just flips <see cref="Status"/> to <see cref="StopStatus.Reached"/>.
/// </summary>
public sealed class Stop
{
    public Guid Id { get; private set; }
    public Guid? ShipmentId { get; private set; }
    public Guid? TruckingCompanyId { get; private set; }
    public StopKind Kind { get; private set; }
    public StopStatus Status { get; private set; }
    public GeoLocation Location { get; private set; } = null!;

    /// <summary>
    /// Gap-based route order (10/20/30...) so a mid-route insertion only needs a value
    /// between its neighbors. See <c>Trip.SequenceForInsertAt</c> for the exhausted-gap fallback.
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>
    /// Distance of the hop from this stop's immediate predecessor to this stop (not
    /// cumulative). Overwritten when a stop is inserted immediately before this one.
    /// </summary>
    public double IncomingLegDistanceKm { get; private set; }

    /// <summary>Time for <see cref="IncomingLegDistanceKm"/>'s hop, in fixed 5-minute ticks.</summary>
    public int IncomingLegTimeTick { get; private set; }

    /// <summary>When the truck actually reached this stop. Null until <see cref="Status"/> is Reached.</summary>
    public DateTime? ReachedAt { get; private set; }

    /// <summary>
    /// Planned wait at this stop, in 5-minute ticks, before the truck may act on it and
    /// depart - because its time window has not opened yet. Set by the feasibility walk
    /// (see <c>RouteEtaCalculator</c>), re-set on later insertions. 0 for Office stops and
    /// on-time arrivals.
    /// </summary>
    public int WaitTimeTick { get; private set; }

    /// <summary>
    /// Ticks of <see cref="WaitTimeTick"/> already served - persists so a wait can span
    /// multiple simulation-advance calls. Reset by <see cref="PlanWait"/>.
    /// </summary>
    public int WaitTimeTickElapsed { get; private set; }

    /// <summary>
    /// The shipment's load, carried on both its Pickup and Delivery stops - so "still on
    /// board" (Pickup Reached, Delivery Pending) needs no reach into the Shipment
    /// aggregate. Null for Office stops.
    /// </summary>
    public Capacity? ShipmentLoad { get; private set; }

    // EF Core materializer only - the factories below are the sole construction path for app code.
    private Stop()
    {
    }

    public static Stop ForShipment(
        Guid shipmentId,
        Capacity shipmentLoad,
        StopKind kind,
        GeoLocation location,
        int sequence,
        double incomingLegDistanceKm,
        int incomingLegTimeTick)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        ArgumentNullException.ThrowIfNull(shipmentLoad);
        ArgumentNullException.ThrowIfNull(location);

        if (kind is not (StopKind.Pickup or StopKind.Delivery))
        {
            throw new ArgumentException("A shipment stop must be a Pickup or a Delivery.", nameof(kind));
        }

        return new Stop
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            TruckingCompanyId = null,
            Kind = kind,
            Status = StopStatus.Pending,
            Location = location,
            Sequence = sequence,
            IncomingLegDistanceKm = incomingLegDistanceKm,
            IncomingLegTimeTick = incomingLegTimeTick,
            ReachedAt = null,
            WaitTimeTick = 0,
            WaitTimeTickElapsed = 0,
            ShipmentLoad = shipmentLoad,
        };
    }

    public static Stop ForOffice(
        Guid truckingCompanyId,
        GeoLocation location,
        int sequence,
        double incomingLegDistanceKm,
        int incomingLegTimeTick)
    {
        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        ArgumentNullException.ThrowIfNull(location);

        return new Stop
        {
            Id = Guid.NewGuid(),
            ShipmentId = null,
            TruckingCompanyId = truckingCompanyId,
            Kind = StopKind.Office,
            Status = StopStatus.Pending,
            Location = location,
            Sequence = sequence,
            IncomingLegDistanceKm = incomingLegDistanceKm,
            IncomingLegTimeTick = incomingLegTimeTick,
            ReachedAt = null,
            WaitTimeTick = 0,
            WaitTimeTickElapsed = 0,
        };
    }

    /// <summary>Replaces this stop's incoming leg - when a new stop is inserted immediately before it.</summary>
    internal void ReplaceIncomingLeg(double incomingLegDistanceKm, int incomingLegTimeTick)
    {
        IncomingLegDistanceKm = incomingLegDistanceKm;
        IncomingLegTimeTick = incomingLegTimeTick;
    }

    /// <summary>Reassigns this stop's Sequence - only via the gap-collision renumbering fallback (see Trip.RenumberStops); never part of ordinary insertion.</summary>
    internal void Renumber(int sequence) => Sequence = sequence;

    /// <summary>
    /// Sets the planned wait (5-minute ticks) and resets the served count. Throws if the
    /// stop is already Reached - its wait is settled.
    /// </summary>
    internal void PlanWait(int waitTimeTick)
    {
        if (waitTimeTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waitTimeTick), waitTimeTick, "Planned wait cannot be negative.");
        }

        if (Status == StopStatus.Reached)
        {
            throw new InvalidOperationException($"Stop '{Id}' has already been reached - its wait cannot be re-planned.");
        }

        WaitTimeTick = waitTimeTick;
        WaitTimeTickElapsed = 0;
    }

    /// <summary>Serves <paramref name="ticks"/> more of the planned wait, capped at <see cref="WaitTimeTick"/>.</summary>
    internal void AccrueWait(int ticks)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "Cannot accrue a negative wait.");
        }

        WaitTimeTickElapsed = Math.Min(WaitTimeTick, WaitTimeTickElapsed + ticks);
    }

    /// <summary>True once the truck has served this stop's full planned wait (trivially true when there is none).</summary>
    public bool IsWaitComplete => WaitTimeTickElapsed >= WaitTimeTick;

    internal void MarkReached(DateTime reachedAt)
    {
        if (Status == StopStatus.Reached)
        {
            throw new InvalidOperationException($"Stop '{Id}' has already been reached.");
        }

        Status = StopStatus.Reached;
        ReachedAt = reachedAt;
    }

    /// <summary>
    /// An independent copy for <see cref="Trip.Clone"/>'s insertion preview - keeps the
    /// same Id (a scratch copy, never persisted). Immutable value objects
    /// (Location/ShipmentLoad) are shared; only the mutable scalars are copied.
    /// </summary>
    internal Stop Clone() => new()
    {
        Id = Id,
        ShipmentId = ShipmentId,
        TruckingCompanyId = TruckingCompanyId,
        Kind = Kind,
        Status = Status,
        Location = Location,
        Sequence = Sequence,
        IncomingLegDistanceKm = IncomingLegDistanceKm,
        IncomingLegTimeTick = IncomingLegTimeTick,
        ReachedAt = ReachedAt,
        WaitTimeTick = WaitTimeTick,
        WaitTimeTickElapsed = WaitTimeTickElapsed,
        ShipmentLoad = ShipmentLoad,
    };
}
