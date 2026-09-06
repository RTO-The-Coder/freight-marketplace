using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// A truck's journey from its office, through the Pickup/Delivery stops assigned along
/// the way, back to office. Opens on the truck's first shipment; closes when it reaches
/// the Office(return) stop. Never deleted, and its <see cref="Stop"/>s are only ever
/// marked <see cref="StopStatus.Reached"/>, never removed - so a Trip is a permanent
/// record of the whole journey, done and still planned.
/// </summary>
public sealed class Trip
{
    private readonly List<Stop> _stops = [];

    public Guid Id { get; private set; }

    /// <summary>Loose reference, no navigation back to Truck - like <see cref="Stop.ShipmentId"/>.</summary>
    public Guid TruckId { get; private set; }

    /// <summary>The truck's company when the trip opened - used to build the Office(return) stop.</summary>
    public Guid TruckingCompanyId { get; private set; }

    /// <summary>
    /// Planned departure - set by the caller, never stamped to "now" (the truck may leave
    /// later, e.g. to match a pickup window). Adjustable pre-departure via <see cref="Reschedule"/>.
    /// </summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>Null while in progress; set when the truck reaches its Office(return) stop.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Actual distance covered so far: the incoming-leg distance of every Reached Stop, plus
    /// partial-leg distance banked when a mid-route insertion abandons an in-progress leg
    /// (<see cref="BankPartialLeg"/>) - which is why it is stored, not summed after the fact.
    /// </summary>
    public double DistanceTravelledSoFar { get; private set; }

    /// <summary>Same as <see cref="DistanceTravelledSoFar"/>, for time (5-minute ticks).</summary>
    public int TimeElapsedSoFar { get; private set; }

    /// <summary>Ordered by <see cref="Stop.Sequence"/>, not by insertion order. Never has entries removed.</summary>
    public IReadOnlyList<Stop> Stops => [.. _stops.OrderBy(stop => stop.Sequence)];

    /// <summary>Planned distance for the whole journey - a read-time sum over every Stop, never stored.</summary>
    public double TotalPlannedDistanceKm => _stops.Sum(stop => stop.IncomingLegDistanceKm);

    /// <summary>Same as <see cref="TotalPlannedDistanceKm"/>, for time.</summary>
    public int TotalPlannedTimeTick => _stops.Sum(stop => stop.IncomingLegTimeTick);

    public bool IsOpen => CompletedAt is null;

    /// <summary>Nearest stop still ahead of the truck on this trip - null if every stop has been reached.</summary>
    public Stop? NextStop => Stops.FirstOrDefault(stop => stop.Status == StopStatus.Pending);

    /// <summary>Whether the truck's immediate next stop on this trip is the Office(return) stop.</summary>
    public bool IsAtOffice => NextStop?.Kind == StopKind.Office;

    /// <summary>
    /// Load currently on board: the sum of <see cref="Stop.ShipmentLoad"/> for every
    /// shipment whose Pickup stop is Reached but whose Delivery stop is still Pending.
    /// </summary>
    public Capacity CurrentLoad
    {
        get
        {
            double weight = 0;
            double volume = 0;

            foreach (var stop in Stops.Where(stop => stop.Status == StopStatus.Reached))
            {
                // Only Pickup/Delivery stops carry a shipment load - Office stops have none.
                if (stop.Kind is not (StopKind.Pickup or StopKind.Delivery))
                {
                    continue;
                }

                // A Pickup/Delivery stop with a null load is corrupt data (see
                // Stop.ForShipment) - fail loudly rather than silently under-counting.
                var load = stop.ShipmentLoad
                    ?? throw new InvalidOperationException($"{stop.Kind} stop '{stop.Id}' has no ShipmentLoad.");

                if (stop.Kind == StopKind.Pickup)
                {
                    weight += load.WeightKg;
                    volume += load.VolumeCubicMeters;
                }
                else
                {
                    weight -= load.WeightKg;
                    volume -= load.VolumeCubicMeters;
                }
            }

            return Capacity.Create(weight, volume);
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
    /// Changes the planned departure. Only while the trip is open, no stop is reached, and
    /// the truck has not started driving (<paramref name="truckHasStartedDriving"/> - the
    /// caller derives this from <see cref="Truck.CurrentProgress"/>).
    /// </summary>
    public void Reschedule(DateTime newStart, bool truckHasStartedDriving)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException($"Trip '{Id}' has completed - its start time cannot be changed.");
        }

        if (_stops.Any(stop => stop.Status == StopStatus.Reached))
        {
            throw new InvalidOperationException(
                $"Trip '{Id}' has already reached a stop - its start time cannot be changed.");
        }

        if (truckHasStartedDriving)
        {
            throw new InvalidOperationException(
                $"Trip '{Id}'s truck is already driving - its start time cannot be changed.");
        }

        StartedAt = newStart;
    }

    /// <summary>
    /// A deep, independent copy for what-if insertion previews (see
    /// <c>IShipmentInsertionEvaluator</c>): run <see cref="AssignShipment"/> on the clone,
    /// check feasibility from its Stops, then discard it - never persisted or tracked.
    /// Stops are copied via <see cref="Stop.Clone"/>. Existing stops keep their ids, but
    /// stops the clone then inserts get fresh ids that will NOT match the ids the real
    /// trip's own <see cref="AssignShipment"/> produces - carry per-stop results back by
    /// <see cref="StopRef"/>, not by stop id.
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
    /// Banks distance/time covered on a leg being abandoned (a stop inserted ahead of the
    /// truck's live position). Must be called before the truck's RouteProgress is
    /// replaced, or that partial progress is lost.
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

    /// <summary>
    /// Sets each Pending Pickup/Delivery stop's planned wait-for-window (5-minute ticks),
    /// keyed by <see cref="StopRef"/> - the output of the feasibility walk. Stops absent
    /// from <paramref name="waitTicksByStop"/> are reset to no wait; Reached and Office
    /// stops are untouched. Called after a feasible insertion is committed.
    /// </summary>
    public void SetPlannedWaits(IReadOnlyDictionary<StopRef, int> waitTicksByStop)
    {
        ArgumentNullException.ThrowIfNull(waitTicksByStop);

        foreach (var stop in _stops.Where(stop =>
                     stop.Status == StopStatus.Pending &&
                     stop.Kind is StopKind.Pickup or StopKind.Delivery))
        {
            stop.PlanWait(waitTicksByStop.TryGetValue(StopRef.For(stop), out var ticks) ? ticks : 0);
        }
    }

    /// <summary>
    /// Serves <paramref name="ticks"/> more of a stop's planned wait-for-window - called
    /// once per parked simulation tick while the truck waits for the window to open.
    /// </summary>
    public void AccrueStopWait(Guid stopId, int ticks)
    {
        var stop = _stops.FirstOrDefault(stop => stop.Id == stopId)
            ?? throw new InvalidOperationException($"Stop '{stopId}' does not belong to this trip.");

        stop.AccrueWait(ticks);
    }

    /// <summary>
    /// Marks a stop Reached, folds its incoming-leg distance/time into the running totals,
    /// and - if it is the Office stop - completes the trip (sets <see cref="CompletedAt"/>).
    /// </summary>
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

    /// <summary>Fixed sequence for the trip's single Office(return) stop - always last, above any gap-based value.</summary>
    private const int OfficeStopSequence = 1000;

    /// <summary>
    /// Inserts a Pickup + Delivery stop pair for a shipment (hop-splitting rule): each new
    /// stop's incoming leg comes from <paramref name="legPlan"/>, and the stop that
    /// previously followed each insertion point has its own incoming leg overwritten to
    /// start from the new stop (<see cref="LegPlan.PickupToFollower"/> /
    /// <see cref="LegPlan.DeliveryToFollower"/>).
    /// <paramref name="pickupInsertIndex"/> / <paramref name="deliveryInsertIndex"/> index
    /// the trip's Pending non-Office stops only. The Office(return) stop always stays last
    /// and is created here (via <paramref name="officeLocation"/>) on the first shipment.
    /// Does not set wait-for-window - see <see cref="SetPlannedWaits"/>.
    /// </summary>
    public void AssignShipment(
        Guid shipmentId,
        Capacity shipmentSize,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        GeoLocation officeLocation,
        int pickupInsertIndex,
        int deliveryInsertIndex,
        LegPlan legPlan)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        ArgumentNullException.ThrowIfNull(shipmentSize);
        ArgumentNullException.ThrowIfNull(pickupLocation);
        ArgumentNullException.ThrowIfNull(deliveryLocation);
        ArgumentNullException.ThrowIfNull(officeLocation);
        ArgumentNullException.ThrowIfNull(legPlan);

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
            SequenceForInsertAt(pendingStops, pickupInsertIndex),
            legPlan.PickupIncoming.DistanceKm, legPlan.PickupIncoming.TimeTick);
        InsertStop(pickupStop, pendingStops, pickupInsertIndex, legPlan.PickupToFollower);

        // deliveryInsertIndex was expressed against the pre-insertion route; the pickup
        // insertion above shifted every original index at/after pickupInsertIndex right
        // by one, so account for that shift before inserting delivery.
        var pendingStopsAfterPickup = PendingNonOfficeStops();
        var deliveryStop = Stop.ForShipment(
            shipmentId, Capacity.Create(shipmentSize.WeightKg, shipmentSize.VolumeCubicMeters), StopKind.Delivery, deliveryLocation,
            SequenceForInsertAt(pendingStopsAfterPickup, deliveryInsertIndex + 1),
            legPlan.DeliveryIncoming.DistanceKm, legPlan.DeliveryIncoming.TimeTick);
        InsertStop(deliveryStop, pendingStopsAfterPickup, deliveryInsertIndex + 1, legPlan.DeliveryToFollower);

        EnsureOfficeStop(officeLocation, legPlan.ToOffice.DistanceKm, legPlan.ToOffice.TimeTick);
    }

    /// <summary>
    /// Adds <paramref name="newStop"/> and, if a stop follows the insertion point,
    /// overwrites that follower's incoming leg with <paramref name="followerIncomingLeg"/>
    /// (the hop from <paramref name="newStop"/>). <paramref name="followerIncomingLeg"/>
    /// must be non-null exactly when <paramref name="index"/> is not past the end.
    /// </summary>
    private void InsertStop(Stop newStop, IReadOnlyList<Stop> orderedNeighbors, int index, RouteSegment? followerIncomingLeg)
    {
        _stops.Add(newStop);

        if (index >= orderedNeighbors.Count)
        {
            return;
        }

        if (followerIncomingLeg is null)
        {
            throw new ArgumentNullException(
                nameof(followerIncomingLeg),
                $"A stop follows insertion index {index}, so its rewritten incoming leg must be supplied.");
        }

        orderedNeighbors[index].ReplaceIncomingLeg(followerIncomingLeg.DistanceKm, followerIncomingLeg.TimeTick);
    }

    private List<Stop> PendingNonOfficeStops() =>
        [.. Stops.Where(stop => stop.Kind != StopKind.Office && stop.Status == StopStatus.Pending)];

    /// <summary>Adds the trip's single Office(return) stop - a no-op if one already exists.</summary>
    private void EnsureOfficeStop(GeoLocation officeLocation, double legDistanceKm, int legTimeTick)
    {
        if (_stops.Any(stop => stop.Kind == StopKind.Office))
        {
            return;
        }

        _stops.Add(Stop.ForOffice(TruckingCompanyId, officeLocation, OfficeStopSequence, legDistanceKm, legTimeTick));
    }

    /// <summary>
    /// A gap-based <see cref="Stop.Sequence"/> for inserting at <paramref name="index"/> -
    /// the midpoint of its neighbors, or <see cref="SequenceGap"/> past the first/last.
    /// Falls back to <see cref="RenumberStops"/> if the gap is exhausted.
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
    /// Rare self-healing fallback: renumbers every non-Office stop to fresh evenly-spaced
    /// values when repeated same-slot insertion has exhausted a <see cref="SequenceGap"/>.
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
