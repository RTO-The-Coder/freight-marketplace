using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class Truck
{
    private const int SequenceGap = 10;

    /// <summary>
    /// Fixed sequence for the route's single Office stop - always the last stop on the
    /// route, comfortably above anything <see cref="SequenceGap"/>-based numbering will
    /// produce for a Pickup/Delivery stop, so it never needs to be bumped.
    /// </summary>
    private const int OfficeStopSequence = 1000;

    private readonly List<Stop> _stops = [];

    public Guid Id { get; private set; }
    public string TruckName { get; private set; } = null!;

    /// <summary>Nullable - a truck can exist before it belongs to any company.</summary>
    public Guid? TruckingCompanyId { get; private set; }

    /// <summary>
    /// Administrative flag - "can this truck accept shipments at all". Separate from the
    /// derived operational <see cref="Status"/>. Always false while
    /// <see cref="TruckingCompanyId"/> is null.
    /// </summary>
    public bool IsActive { get; private set; }

    public TruckType TruckType { get; private set; }
    public TruckSize TruckSize { get; private set; }

    /// <summary>Derived from <see cref="TruckSize"/> at creation - never entered independently.</summary>
    public TruckCapacity Capacity { get; private set; } = null!;

    public DriverAssignment? DriverAssignment { get; private set; }
    public bool HazmatCertified { get; private set; }

    /// <summary>Ordered by <see cref="Stop.Sequence"/>, not by insertion order.</summary>
    public IReadOnlyList<Stop> Stops => [.. _stops.OrderBy(stop => stop.Sequence)];

    /// <summary>
    /// Progress along the current route leg. Null until the truck starts its first leg
    /// - see <see cref="StartLeg"/>. <see cref="RouteProgress.TotalTimeTick"/> is
    /// expressed in fixed 5-minute ticks (e.g. 6h30m = 390 minutes = 78 ticks), not
    /// seconds.
    /// </summary>
    public RouteProgress? CurrentProgress { get; private set; }

    /// <summary>
    /// Operational state, derived from the route and driver assignment - see
    /// <see cref="DetermineStatus"/>. Never set directly.
    /// </summary>
    public TruckStatus Status => DetermineStatus();

    /// <summary>
    /// Capacity still available right now, derived from <see cref="Capacity"/>.Total
    /// minus the sum of <see cref="Stop.ShipmentLoad"/> across Pickup stops still on the
    /// route (a shipment's load counts against remaining capacity from the moment it's
    /// assigned until its Delivery stop is reached and <see cref="RemoveShipment"/>
    /// takes both stops off the route). Deliberately not stored - see
    /// <see cref="ValueObjects.TruckCapacity"/>.
    /// </summary>
    public Capacity RemainingCapacity
    {
        get
        {
            var assignedWeight = 0.0;
            var assignedVolume = 0.0;

            foreach (var stop in _stops)
            {
                if (stop.Kind == StopKind.Pickup && stop.ShipmentLoad is { } load)
                {
                    assignedWeight += load.WeightKg;
                    assignedVolume += load.VolumeCubicMeters;
                }
            }

            // "ValueObjects.Capacity" disambiguates the type from the Truck.Capacity
            // (TruckCapacity) property of the same simple name, in scope here.
            return Capacity.Total.Subtract(ValueObjects.Capacity.Create(assignedWeight, assignedVolume));
        }
    }

    // EF Core cannot bind capacity/driverAssignment through the constructor below
    // (they are owned-type/reference navigations, and EF's constructor injection only
    // binds scalar properties) - this parameterless constructor exists solely so EF's
    // materializer can construct an instance and set the properties above via
    // reflection. Create(...) remains the only construction path reachable from
    // application code.
    private Truck()
    {
    }

    private Truck(Guid id, string truckName, TruckType truckType, TruckSize truckSize, TruckCapacity capacity)
    {
        Id = id;
        TruckName = truckName;
        TruckType = truckType;
        TruckSize = truckSize;
        Capacity = capacity;
        TruckingCompanyId = null;
        IsActive = false;
        DriverAssignment = null;
        HazmatCertified = false;
    }

    public static Truck Create(string truckName, TruckType type, TruckSize size) =>
        Create(Guid.NewGuid(), truckName, type, size);

    public static Truck Create(Guid id, string truckName, TruckType type, TruckSize size)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Truck id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(truckName))
        {
            throw new ArgumentException("Truck name is required.", nameof(truckName));
        }

        return new Truck(id, truckName, type, size, new TruckCapacity(ValueObjects.Capacity.ForTruckSize(size)));
    }

    public void CertifyForHazmat() => HazmatCertified = true;

    public void RevokeHazmatCertification() => HazmatCertified = false;

    public void AssignToCompany(Guid truckingCompanyId)
    {
        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        TruckingCompanyId = truckingCompanyId;
    }

    public void UnassignFromCompany()
    {
        TruckingCompanyId = null;
        IsActive = false;
    }

    public void Activate()
    {
        if (TruckingCompanyId is null)
        {
            throw new InvalidOperationException("A truck cannot be activated before it is assigned to a trucking company.");
        }

        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Assigns one or two drivers. A secondary driver is only permitted on
    /// <see cref="TruckSize.Large"/> trucks. Replaces any existing assignment, which
    /// resets the active-driver pointer.
    /// </summary>
    public void AssignDrivers(Driver primaryDriver, Driver? secondaryDriver = null)
    {
        ArgumentNullException.ThrowIfNull(primaryDriver);

        DriverAssignment = secondaryDriver is null
            ? DriverAssignment.Single(primaryDriver)
            : DriverAssignment.Team(primaryDriver, secondaryDriver, TruckSize);
    }

    /// <summary>
    /// Sets which assigned driver is currently at the wheel. Delegates the
    /// one-directional stickiness invariant to <see cref="Fleet.DriverAssignment"/>.
    /// </summary>
    public void SetActiveDriver(Guid? driverId)
    {
        if (DriverAssignment is null)
        {
            throw new InvalidOperationException("Cannot set the active driver before drivers are assigned to this truck.");
        }

        DriverAssignment.AdvanceActiveDriver(driverId);
    }

    /// <summary>
    /// Derives the truck's operational status: <see cref="TruckStatus.Idle"/> when no
    /// assigned driver can currently drive, <see cref="TruckStatus.AtOffice"/> when the
    /// next stop on the route is an Office stop, otherwise <see cref="TruckStatus.Running"/>.
    /// </summary>
    public TruckStatus DetermineStatus()
    {
        if (DriverAssignment is null || !DriverAssignment.HasDriverAbleToDrive)
        {
            return TruckStatus.Idle;
        }

        var orderedStops = Stops;

        if (orderedStops.Count > 0 && orderedStops[0].Kind == StopKind.Office)
        {
            return TruckStatus.AtOffice;
        }

        return TruckStatus.Running;
    }

    /// <summary>
    /// Inserts a Pickup + Delivery stop pair for a shipment. <paramref name="pickupInsertIndex"/>
    /// and <paramref name="deliveryInsertIndex"/> are positions among the route's
    /// non-Office stops only - the route's single Office stop (see
    /// <see cref="OfficeStopSequence"/>) always stays last regardless of these indices,
    /// and is created here via <paramref name="officeLocation"/> the first time this
    /// truck receives a shipment, if it doesn't already have one.
    /// </summary>
    public void AssignShipment(
        Guid shipmentId,
        Capacity shipmentSize,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        GeoLocation officeLocation,
        int pickupInsertIndex,
        int deliveryInsertIndex,
        DateTime pickupExpectedArrivalTime,
        DateTime deliveryExpectedArrivalTime)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        ArgumentNullException.ThrowIfNull(shipmentSize);
        ArgumentNullException.ThrowIfNull(pickupLocation);
        ArgumentNullException.ThrowIfNull(deliveryLocation);
        ArgumentNullException.ThrowIfNull(officeLocation);

        var nonOfficeStops = NonOfficeStops();

        if (pickupInsertIndex < 0 || pickupInsertIndex > nonOfficeStops.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pickupInsertIndex), pickupInsertIndex,
                "Pickup insertion index is out of range for the current route.");
        }

        if (deliveryInsertIndex < 0 || deliveryInsertIndex > nonOfficeStops.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryInsertIndex), deliveryInsertIndex,
                "Delivery insertion index is out of range for the current route.");
        }

        if (deliveryInsertIndex < pickupInsertIndex)
        {
            throw new ArgumentException(
                "Delivery must be inserted at or after pickup in the route.", nameof(deliveryInsertIndex));
        }

        if (!RemainingCapacity.CanAccommodate(shipmentSize))
        {
            throw new InvalidOperationException(
                $"{TruckName} does not have enough remaining capacity for this shipment " +
                $"({RemainingCapacity.WeightKg}kg / {RemainingCapacity.VolumeCubicMeters}m³ available).");
        }

        var pickupSequence = SequenceForInsertAt(nonOfficeStops, pickupInsertIndex);
        _stops.Add(Stop.ForShipment(shipmentId, shipmentSize, StopKind.Pickup, pickupLocation, pickupSequence, pickupExpectedArrivalTime));

        // deliveryInsertIndex was expressed against the pre-insertion route; the pickup
        // insertion above shifted every original index at/after pickupInsertIndex right
        // by one, so account for that shift before inserting delivery.
        var deliverySequence = SequenceForInsertAt(NonOfficeStops(), deliveryInsertIndex + 1);
        _stops.Add(Stop.ForShipment(shipmentId, shipmentSize, StopKind.Delivery, deliveryLocation, deliverySequence, deliveryExpectedArrivalTime));

        EnsureOfficeStop(officeLocation);
    }

    /// <summary>
    /// Starts (or restarts) progress on the truck's current route leg - constructs
    /// <see cref="CurrentProgress"/> the first time this is called, or resets it via
    /// <see cref="RouteProgress.StartNewLeg"/> on every call after. No OSRM/routing
    /// integration exists yet (Slice 7), so callers currently pass hardcoded
    /// placeholder values rather than a real computed distance/time.
    /// </summary>
    public void StartLeg(double totalDistanceKm, int totalTimeTick)
    {
        if (CurrentProgress is null)
        {
            CurrentProgress = new RouteProgress(totalDistanceKm, totalTimeTick);
        }
        else
        {
            CurrentProgress.StartNewLeg(totalDistanceKm, totalTimeTick);
        }
    }

    private List<Stop> NonOfficeStops() =>
        [.. Stops.Where(stop => stop.Kind != StopKind.Office)];

    /// <summary>
    /// Ensures this truck's route has its single, always-last Office stop - a no-op if
    /// one already exists. <see cref="ExpectedArrivalTime"/> is a Phase 1 placeholder
    /// (no OSRM/ETA engine wired up yet), matching every other stop's expected-arrival
    /// value.
    /// </summary>
    private void EnsureOfficeStop(GeoLocation officeLocation)
    {
        if (_stops.Any(stop => stop.Kind == StopKind.Office))
        {
            return;
        }

        if (TruckingCompanyId is null)
        {
            throw new InvalidOperationException("A truck without a trucking company has no office to stop at.");
        }

        _stops.Add(Stop.ForOffice(TruckingCompanyId.Value, officeLocation, OfficeStopSequence, DateTime.UtcNow));
    }

    public void RemoveShipment(Guid shipmentId)
    {
        _stops.RemoveAll(stop => stop.ShipmentId == shipmentId);
    }

    /// <summary>
    /// Computes a gap-based <see cref="Stop.Sequence"/> value for inserting a new stop at
    /// <paramref name="index"/> among <paramref name="orderedStops"/> (already
    /// Sequence-ordered) - the midpoint between its two neighbors, or
    /// <see cref="SequenceGap"/> before/after the first/last stop. No renumbering
    /// fallback: Phase 1's insertion volumes are far too low to exhaust the available
    /// integer gaps between neighbors.
    /// </summary>
    private static int SequenceForInsertAt(IReadOnlyList<Stop> orderedStops, int index)
    {
        var before = index > 0 ? orderedStops[index - 1].Sequence : (int?)null;
        var after = index < orderedStops.Count ? orderedStops[index].Sequence : (int?)null;

        return (before, after) switch
        {
            (null, null) => SequenceGap,
            (null, { } a) => a - SequenceGap,
            ({ } b, null) => b + SequenceGap,
            ({ } b, { } a) => b + (a - b) / 2,
        };
    }
}
