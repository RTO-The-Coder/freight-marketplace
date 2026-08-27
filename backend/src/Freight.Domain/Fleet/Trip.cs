using System.Runtime.CompilerServices;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// A truck's full journey from leaving its office, through however many Pickup/Delivery
/// stops get assigned along the way, back to office. Opens when an idle truck (no
/// currently-open trip) is assigned its first shipment; closes when the truck reaches
/// its Office(return) stop. Never deleted - <see cref="Stop"/>s belonging to a Trip are
/// never removed either, only marked <see cref="StopStatus.Reached"/>, so a Trip is a
/// permanent, always-queryable record of everything the truck has done and is still
/// planning to do on this journey.
/// </summary>
public sealed class Trip
{
    private readonly List<Stop> _stops = [];

    public Guid Id { get; private set; }

    /// <summary>Plain reference, no forced navigation back to Truck - same loose-reference style as <see cref="Stop.ShipmentId"/>.</summary>
    public Guid TruckId { get; private set; }

    /// <summary>
    /// The trucking company this trip's truck belonged to at the time it opened - needed
    /// only to construct the trip's Office(return) stop (see <see cref="Stop.ForOffice"/>).
    /// </summary>
    public Guid TruckingCompanyId { get; private set; }

    /// <summary>
    /// Planned departure time - supplied by the caller opening the trip, never stamped
    /// to "now" (the truck may not actually leave until later, e.g. to match a
    /// shipment's pickup window).
    /// </summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>Null while the trip is in progress; set when the truck reaches its Office(return) stop.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Running total of ACTUAL distance covered so far - accumulates the incoming-leg
    /// distance of every Stop that flips to Reached, plus any partial-leg distance
    /// banked when an in-progress leg is abandoned and replaced by a mid-route
    /// insertion (see <see cref="BankPartialLeg"/>). Not derivable purely by summing
    /// Stops after the fact, because banked partial-leg distance isn't attributable to
    /// any single Stop.
    /// </summary>
    public double DistanceTravelledSoFar { get; private set; }

    /// <summary>Same as <see cref="DistanceTravelledSoFar"/>, for time (5-minute ticks).</summary>
    public int TimeElapsedSoFar { get; private set; }

    /// <summary>Ordered by <see cref="Stop.Sequence"/>, not by insertion order. Never has entries removed.</summary>
    public IReadOnlyList<Stop> Stops => [.. _stops.OrderBy(stop => stop.Sequence)];

    /// <summary>
    /// Total planned distance for the whole journey - always a derived read-time sum
    /// across every Stop (Pending + Reached), never stored, since Stops are never gone.
    /// </summary>
    public double TotalPlannedDistanceKm => _stops.Sum(stop => stop.IncomingLegDistanceKm);

    /// <summary>Same as <see cref="TotalPlannedDistanceKm"/>, for time.</summary>
    public int TotalPlannedTimeTick => _stops.Sum(stop => stop.IncomingLegTimeTick);

    public bool IsOpen => CompletedAt is null;

    /// <summary>Nearest stop still ahead of the truck on this trip - null if every stop has been reached.</summary>
    public Stop? NextStop => Stops.FirstOrDefault(stop => stop.Status == StopStatus.Pending);

    /// <summary>Whether the truck's immediate next stop on this trip is the Office(return) stop.</summary>
    public bool IsAtOffice => NextStop?.Kind == StopKind.Office;

    /// <summary>
    /// Load currently on board, derived from this trip's own stops - the sum of
    /// <see cref="Stop.ShipmentLoad"/> for every shipment whose Pickup stop is Reached
    /// but whose matching Delivery stop is still Pending. Stops are never deleted, so
    /// "does the Pickup stop still exist" can no longer answer "is it still on board" the
    /// way it used to; this Reached-Pickup/Pending-Delivery pairing replaces that check.
    /// </summary>
    public Capacity CurrentLoad
    {
        get
        {
            var reachedStops = Stops.Where(x => x.Status == StopStatus.Reached);
            double weight = 0;
            double volumn = 0;
            foreach (var stop in reachedStops)
            {
                if (stop.Kind == StopKind.Pickup)
                {
                    weight += stop.ShipmentLoad.WeightKg;
                    volumn += stop.ShipmentLoad.VolumeCubicMeters;
                }
                else
                {
                    weight -= stop.ShipmentLoad.WeightKg;
                    volumn -= stop.ShipmentLoad.VolumeCubicMeters;
                }
            }
            return Capacity.Create(weight, volumn);
        }
    }

    // EF Core materializer only - see the equivalent comment on TruckingCompany's
    // parameterless constructor.
    private Trip()
    {
    }

    private Trip(Guid id, Guid truckId, Guid truckingCompanyId, DateTime startedAt)
    {
        Id = id;
        TruckId = truckId;
        TruckingCompanyId = truckingCompanyId;
        StartedAt = startedAt;
        CompletedAt = null;
        DistanceTravelledSoFar = 0;
        TimeElapsedSoFar = 0;
    }

    public static Trip Open(Guid truckId, Guid truckingCompanyId, DateTime startedAt)
    {
        if (truckId == Guid.Empty)
        {
            throw new ArgumentException("Truck id cannot be empty.", nameof(truckId));
        }

        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        return new Trip(Guid.NewGuid(), truckId, truckingCompanyId, startedAt);
    }

    /// <summary>
    /// A full, independent copy - for a what-if insertion preview (see
    /// IShipmentInsertionEvaluator): call the real <see cref="AssignShipment"/> on the
    /// clone, inspect the resulting Stops to check feasibility, then discard the clone
    /// (or, if feasible, run the same AssignShipment call for real on the original) -
    /// never used to mutate the real, tracked Trip. Same identity (Id/TruckId) as the
    /// original: this is a scratch copy of one Trip's state, never persisted or queried
    /// as a second real Trip. Stops are deep-copied via <see cref="Stop.Clone"/> so that
    /// mutating the clone's stops (inserting new ones, overwriting incoming legs) never
    /// touches the original's stops.
    /// </summary>
    public Trip Clone()
    {
        var clone = new Trip(Id, TruckId, TruckingCompanyId, StartedAt)
        {
            CompletedAt = CompletedAt,
            DistanceTravelledSoFar = DistanceTravelledSoFar,
            TimeElapsedSoFar = TimeElapsedSoFar,
        };

        foreach (var stop in _stops)
        {
            clone._stops.Add(stop.Clone());
        }

        return clone;
    }

    /// <summary>
    /// Banks distance/time already covered on a leg that's being abandoned - e.g. a new
    /// stop is inserted ahead of the truck's live position, replacing the leg it was
    /// mid-way through. Must be called before the truck's RouteProgress is replaced,
    /// otherwise that partial progress is lost (it isn't attributable to any single
    /// Stop once the leg's original target is no longer the immediate next stop).
    /// </summary>
    public void BankPartialLeg(double distanceKm, int timeTick)
    {
        if (distanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceKm), distanceKm, "Banked distance cannot be negative.");
        }

        if (timeTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeTick), timeTick, "Banked time cannot be negative.");
        }

        DistanceTravelledSoFar += distanceKm;
        TimeElapsedSoFar += timeTick;
    }

    /// <summary>Marks a stop Reached and folds its incoming-leg distance/time into the running totals.</summary>
    public void MarkStopReached(Guid stopId, DateTime reachedAt)
    {
        var stop = _stops.FirstOrDefault(stop => stop.Id == stopId)
            ?? throw new InvalidOperationException($"Stop '{stopId}' does not belong to this trip.");

        stop.MarkReached(reachedAt);

        DistanceTravelledSoFar += stop.IncomingLegDistanceKm;
        TimeElapsedSoFar += stop.IncomingLegTimeTick;

        if (stop.Kind == StopKind.Office)
        {
            CompletedAt = reachedAt;
        }
    }

    private const int SequenceGap = 10;

    /// <summary>
    /// Fixed sequence for the trip's single Office(return) stop - always last, comfortably
    /// above anything <see cref="SequenceGap"/>-based numbering will produce for a
    /// Pickup/Delivery stop, so it never needs to be bumped by ordinary insertion.
    /// </summary>
    private const int OfficeStopSequence = 1000;

    /// <summary>
    /// Inserts a Pickup + Delivery stop pair for a shipment, following the general
    /// hop-splitting insertion rule: the new stop's incoming leg is the caller-supplied
    /// distance/time (a placeholder for this slice - no OSRM integration yet), and
    /// whichever stop previously followed the insertion point has its OWN incoming leg
    /// OVERWRITTEN to now mean "from the new stop" instead of "from whatever used to
    /// precede it". <paramref name="pickupInsertIndex"/>/<paramref name="deliveryInsertIndex"/>
    /// are positions among the trip's PENDING, non-Office stops only - Reached stops are
    /// history and are never insertion targets, and the trip's single Office(return) stop
    /// always stays last regardless of these indices (created here via
    /// <paramref name="officeLocation"/> the first time this trip receives a shipment, if
    /// it doesn't already have one).
    /// </summary>
    public void AssignShipment(
        Guid shipmentId,
        Capacity shipmentSize,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        GeoLocation officeLocation,
        int pickupInsertIndex,
        int deliveryInsertIndex,
        double pickupLegDistanceKm,
        int pickupLegTimeTick,
        double deliveryLegDistanceKm,
        int deliveryLegTimeTick,
        double officeLegDistanceKm,
        int officeLegTimeTick)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        ArgumentNullException.ThrowIfNull(shipmentSize);
        ArgumentNullException.ThrowIfNull(pickupLocation);
        ArgumentNullException.ThrowIfNull(deliveryLocation);
        ArgumentNullException.ThrowIfNull(officeLocation);

        var pendingStops = PendingNonOfficeStops();

        if (pickupInsertIndex < 0 || pickupInsertIndex > pendingStops.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pickupInsertIndex), pickupInsertIndex,
                "Pickup insertion index is out of range for the current route.");
        }

        // deliveryInsertIndex, like pickupInsertIndex, is a position among the
        // PRE-insertion pending stops (not the post-pickup-insertion list) - the +1
        // shift applied below is what accounts for pickup's insertion, so this bound
        // must match pendingStops.Count, the same list pickupInsertIndex is bounded
        // against.
        if (deliveryInsertIndex < 0 || deliveryInsertIndex > pendingStops.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryInsertIndex), deliveryInsertIndex,
                "Delivery insertion index is out of range for the current route.");
        }

        if (deliveryInsertIndex < pickupInsertIndex)
        {
            throw new ArgumentException(
                "Delivery must be inserted at or after pickup in the route.", nameof(deliveryInsertIndex));
        }

        // Each stop gets its OWN Capacity instance, never the same shipmentSize reference
        // shared between them - EF Core's change tracker follows owned-type navigations
        // by reference identity, and reusing one Capacity instance as the ShipmentLoad
        // of two different Stop rows makes it treat the second row as "already tracked,
        // nothing changed", silently dropping ShipmentLoad from that row's INSERT (the
        // same class of bug the handler already guards against for GeoLocation/Capacity
        // at its own layer - see AssignShipmentToTruckHandler's fresh-instance comment).
        var pickupStop = Stop.ForShipment(
            shipmentId, Capacity.Create(shipmentSize.WeightKg, shipmentSize.VolumeCubicMeters), StopKind.Pickup, pickupLocation,
            SequenceForInsertAt(pendingStops, pickupInsertIndex), pickupLegDistanceKm, pickupLegTimeTick);
        InsertStop(pickupStop, pendingStops, pickupInsertIndex);

        // deliveryInsertIndex was expressed against the pre-insertion route; the pickup
        // insertion above shifted every original index at/after pickupInsertIndex right
        // by one, so account for that shift before inserting delivery.
        var pendingStopsAfterPickup = PendingNonOfficeStops();
        var deliveryStop = Stop.ForShipment(
            shipmentId, Capacity.Create(shipmentSize.WeightKg, shipmentSize.VolumeCubicMeters), StopKind.Delivery, deliveryLocation,
            SequenceForInsertAt(pendingStopsAfterPickup, deliveryInsertIndex + 1), deliveryLegDistanceKm, deliveryLegTimeTick);
        InsertStop(deliveryStop, pendingStopsAfterPickup, deliveryInsertIndex + 1);

        EnsureOfficeStop(officeLocation, officeLegDistanceKm, officeLegTimeTick);
    }

    /// <summary>
    /// Inserts <paramref name="newStop"/> at <paramref name="index"/> among
    /// <paramref name="orderedNeighbors"/> (the pre-insertion pending/non-office stops)
    /// and, per the general hop-splitting rule, overwrites the incoming leg of whichever
    /// stop now immediately follows it - that stop's hop used to start from
    /// <paramref name="newStop"/>'s predecessor, and now starts from
    /// <paramref name="newStop"/> instead.
    /// </summary>
    private void InsertStop(Stop newStop, IReadOnlyList<Stop> orderedNeighbors, int index)
    {
        _stops.Add(newStop);

        if (index < orderedNeighbors.Count)
        {
            orderedNeighbors[index].ReplaceIncomingLeg(newStop.IncomingLegDistanceKm, newStop.IncomingLegTimeTick);
        }
    }

    private List<Stop> PendingNonOfficeStops() =>
        [.. Stops.Where(stop => stop.Kind != StopKind.Office && stop.Status == StopStatus.Pending)];

    /// <summary>
    /// Ensures this trip has its single, always-last Office(return) stop - a no-op if one
    /// already exists.
    /// </summary>
    private void EnsureOfficeStop(GeoLocation officeLocation, double legDistanceKm, int legTimeTick)
    {
        if (_stops.Any(stop => stop.Kind == StopKind.Office))
        {
            return;
        }

        _stops.Add(Stop.ForOffice(TruckingCompanyId, officeLocation, OfficeStopSequence, legDistanceKm, legTimeTick));
    }

    /// <summary>
    /// Computes a gap-based <see cref="Stop.Sequence"/> value for inserting a new stop at
    /// <paramref name="index"/> among <paramref name="orderedStops"/> (already
    /// Sequence-ordered) - the midpoint between its two neighbors, or
    /// <see cref="SequenceGap"/> before/after the first/last stop. Falls back to
    /// <see cref="RenumberStops"/> when the computed value would collide with a neighbor
    /// (the gap between them has been exhausted by repeated same-slot insertion).
    /// </summary>
    private int SequenceForInsertAt(IReadOnlyList<Stop> orderedStops, int index)
    {
        var before = index > 0 ? orderedStops[index - 1].Sequence : (int?)null;
        var after = index < orderedStops.Count ? orderedStops[index].Sequence : (int?)null;

        var candidate = (before, after) switch
        {
            (null, null) => SequenceGap,
            (null, { } a) => a - SequenceGap,
            ({ } b, null) => b + SequenceGap,
            ({ } b, { } a) => b + (a - b) / 2,
        };

        if ((before is { } b2 && candidate == b2) || (after is { } a2 && candidate == a2))
        {
            RenumberStops();
            return SequenceForInsertAt(PendingNonOfficeStops(), index);
        }

        return candidate;
    }

    /// <summary>
    /// Bounded, rare, self-healing fallback for when a gap-based Sequence value has been
    /// exhausted by repeated insertion into the exact same slot (~4 repeated same-slot
    /// insertions collapse a starting gap of <see cref="SequenceGap"/> via integer
    /// division). Renumbers every stop in the trip to fresh, evenly-spaced values -
    /// does not run on the normal insertion path.
    /// </summary>
    private void RenumberStops()
    {
        var sequence = SequenceGap;

        foreach (var stop in Stops.Where(stop => stop.Kind != StopKind.Office))
        {
            stop.Renumber(sequence);
            sequence += SequenceGap;
        }
    }
}
