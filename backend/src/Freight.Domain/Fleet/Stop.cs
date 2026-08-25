using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// A waypoint on a <see cref="Trip"/>'s route. Owned by the trip - never accessed
/// through a repository of its own. <see cref="ShipmentId"/> is null for Office stops;
/// <see cref="TruckingCompanyId"/> is set only for Office stops. Never deleted - once
/// reached, <see cref="Status"/> flips to <see cref="StopStatus.Reached"/> and the row
/// stays forever, which is what makes the owning Trip a permanent historical record.
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
    /// Gap-based route order (10/20/30...) so a mid-route insertion only needs a
    /// value between its two neighbors, not a renumbering of every later stop. See
    /// <see cref="Truck.SequenceForInsertAt"/> for the fallback when a gap is
    /// exhausted by repeated same-slot insertion.
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>
    /// Distance of the hop FROM this stop's immediate predecessor TO this stop - never
    /// a cumulative/absolute figure. Overwritten whenever a new stop is inserted
    /// immediately before this one (the predecessor changes, so the hop leading here
    /// changes too).
    /// </summary>
    public double IncomingLegDistanceKm { get; private set; }

    /// <summary>Time for <see cref="IncomingLegDistanceKm"/>'s hop, in fixed 5-minute ticks.</summary>
    public int IncomingLegTimeTick { get; private set; }

    /// <summary>When the truck actually reached this stop. Null until <see cref="Status"/> is Reached.</summary>
    public DateTime? ReachedAt { get; private set; }

    /// <summary>
    /// The shipment's load, carried on BOTH its Pickup and Delivery stops (not Pickup
    /// only) - now that stops are never deleted, "is this shipment still on board" can
    /// no longer be inferred from "does its Pickup stop still exist in the route." It's
    /// instead: Pickup is Reached and its matching Delivery is still Pending. Both
    /// stops need to carry the figure so that check doesn't need to reach back into the
    /// Shipment aggregate. Null for Office stops.
    /// </summary>
    public Capacity? ShipmentLoad { get; private set; }

    // EF Core materializes owned entities through a parameterless constructor and sets
    // the properties above via reflection. The factories below remain the only
    // construction path reachable from application code.
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
        };
    }

    /// <summary>Replaces this stop's incoming leg - called when a new stop is inserted immediately before it, per the general hop-splitting insertion rule.</summary>
    internal void ReplaceIncomingLeg(double incomingLegDistanceKm, int incomingLegTimeTick)
    {
        IncomingLegDistanceKm = incomingLegDistanceKm;
        IncomingLegTimeTick = incomingLegTimeTick;
    }

    /// <summary>Reassigns this stop's Sequence - only via the gap-collision renumbering fallback (see Trip.RenumberStops); never part of ordinary insertion.</summary>
    internal void Renumber(int sequence) => Sequence = sequence;

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
    /// A full, independent copy - for <see cref="Trip.Clone"/>'s what-if insertion
    /// preview (see Trip's doc comment). Same identity (Id) as the original: the clone
    /// is a scratch copy of one Trip's state, never persisted or compared against other
    /// Stops, so id collision isn't a concern here the way it would be for a second
    /// independently-persisted row. Location/ShipmentLoad are immutable value objects,
    /// so sharing the same instance is safe - only the mutable scalar fields need an
    /// independent copy for mutations on the clone to never affect the original.
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
        ShipmentLoad = ShipmentLoad,
    };
}
