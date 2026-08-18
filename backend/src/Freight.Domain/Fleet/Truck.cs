using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class Truck
{
    private readonly List<Stop> _routeStops = [];

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
    public IReadOnlyList<Stop> RouteStops => _routeStops;

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

            foreach (var stop in _routeStops)
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

        if (_routeStops.Count > 0 && _routeStops[0].Kind == StopKind.Office)
        {
            return TruckStatus.AtOffice;
        }

        return TruckStatus.Running;
    }

    public void AssignShipment(
        Guid shipmentId,
        Capacity shipmentSize,
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

        if (pickupInsertIndex < 0 || pickupInsertIndex > _routeStops.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pickupInsertIndex), pickupInsertIndex,
                "Pickup insertion index is out of range for the current route.");
        }

        if (deliveryInsertIndex < 0 || deliveryInsertIndex > _routeStops.Count)
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
            throw new InvalidOperationException("Truck does not have sufficient remaining capacity for this shipment.");
        }

        _routeStops.Insert(pickupInsertIndex, Stop.ForShipment(shipmentId, shipmentSize, StopKind.Pickup, pickupExpectedArrivalTime));

        // deliveryInsertIndex was expressed against the pre-insertion route; the pickup
        // insertion above shifted every original index at/after pickupInsertIndex right
        // by one, so account for that shift before inserting delivery.
        _routeStops.Insert(deliveryInsertIndex + 1, Stop.ForShipment(shipmentId, shipmentSize, StopKind.Delivery, deliveryExpectedArrivalTime));
    }

    public void RemoveShipment(Guid shipmentId)
    {
        _routeStops.RemoveAll(stop => stop.ShipmentId == shipmentId);
    }

    /// <summary>
    /// Inserts an Office waypoint into the route - a pure waypoint with no automatic
    /// side effects in this phase.
    /// </summary>
    public void InsertOfficeStop(int insertIndex, DateTime expectedArrivalTime)
    {
        if (TruckingCompanyId is null)
        {
            throw new InvalidOperationException("A truck without a trucking company has no office to stop at.");
        }

        if (insertIndex < 0 || insertIndex > _routeStops.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(insertIndex), insertIndex,
                "Office stop insertion index is out of range for the current route.");
        }

        _routeStops.Insert(insertIndex, Stop.ForOffice(TruckingCompanyId.Value, expectedArrivalTime));
    }
}
