using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class Truck
{
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

    public TruckType Type { get; private set; }
    public TruckSize Size { get; private set; }

    /// <summary>Derived from <see cref="TruckSize"/> at creation - never entered independently.</summary>
    public Capacity Capacity { get; private set; } = null!;

    public DriverAssignment? DriverAssignment { get; private set; }
    public bool HazmatCertified { get; private set; }

    /// <summary>
    /// Progress along the current route leg. Null until the truck starts its first leg
    /// - see <see cref="AssignShipment"/>. <see cref="RouteProgress.TotalTimeTick"/> is
    /// expressed in fixed 5-minute ticks (e.g. 6h30m = 390 minutes = 78 ticks), not
    /// seconds.
    /// </summary>
    public RouteProgress? CurrentProgress { get; private set; }

    /// <summary>
    /// Operational state, derived from the route and driver assignment - see
    /// <see cref="DetermineStatus"/>. Never set directly.
    /// </summary>
    public TruckStatus Status => DetermineStatus();

    // EF Core cannot bind capacity/driverAssignment through the constructor below
    // (they are owned-type/reference navigations, and EF's constructor injection only
    // binds scalar properties) - this parameterless constructor exists solely so EF's
    // materializer can construct an instance and set the properties above via
    // reflection. Create(...) remains the only construction path reachable from
    // application code.
    private Truck()
    {
    }

    private Truck(Guid id, string truckName, TruckType type, TruckSize size, Capacity capacity)
    {
        Id = id;
        TruckName = truckName;
        Type = type;
        Size = size;
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

        return new Truck(id, truckName, type, size, Capacity.ForTruckSize(size));
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
            : DriverAssignment.Team(primaryDriver, secondaryDriver, Size);
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
    /// assigned driver can currently drive, <see cref="TruckStatus.AtOffice"/> when
    /// <paramref name="currentTrip"/> says the truck's next stop is Office, otherwise
    /// <see cref="TruckStatus.Running"/>. Needs the truck's current trip (if any) - a
    /// Truck no longer owns Stops directly, so this overload takes the trip explicitly;
    /// Trip itself answers the route-shape question (<see cref="Trip.IsAtOffice"/>)
    /// since Stops are its data, not Truck's.
    /// </summary>
    public TruckStatus DetermineStatus(Trip? currentTrip = null)
    {
        if (DriverAssignment is null || !DriverAssignment.HasDriverAbleToDrive)
        {
            return TruckStatus.Idle;
        }

        if (currentTrip?.IsAtOffice == true)
        {
            return TruckStatus.AtOffice;
        }

        return TruckStatus.Running;
    }

    /// <summary>
    /// Assigns a shipment onto <paramref name="trip"/> (the truck's current open trip -
    /// opened fresh by the caller if none exists yet) and starts/updates this truck's
    /// live <see cref="CurrentProgress"/> to match. If the insertion changes the trip's
    /// immediate next-Pending-stop while the truck is already mid-leg, the old leg's
    /// progress is banked onto the trip before CurrentProgress is replaced (see
    /// RouteProgress's class doc comment) rather than simply left in place; otherwise
    /// CurrentProgress is left untouched (the insertion landed after the truck's live
    /// position, not ahead of it).
    /// </summary>
    public void AssignShipment(
        Trip trip,
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
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(shipmentSize);

        if (trip.TruckId != Id)
        {
            throw new ArgumentException("This trip does not belong to this truck.", nameof(trip));
        }

        var previousNextStopId = trip.NextStop?.Id;

        trip.AssignShipment(
            shipmentId, shipmentSize, pickupLocation, deliveryLocation, officeLocation,
            pickupInsertIndex, deliveryInsertIndex,
            pickupLegDistanceKm, pickupLegTimeTick,
            deliveryLegDistanceKm, deliveryLegTimeTick,
            officeLegDistanceKm, officeLegTimeTick);

        var newNextStop = trip.NextStop!;

        if (CurrentProgress is null)
        {
            CurrentProgress = new RouteProgress(newNextStop.IncomingLegDistanceKm, newNextStop.IncomingLegTimeTick);
        }
        else if (newNextStop.Id != previousNextStopId)
        {
            // The insertion landed ahead of the truck's live position - the leg it was
            // mid-way through is abandoned. Bank what's already been covered before
            // replacing CurrentProgress with a fresh leg toward the new immediate stop.
            trip.BankPartialLeg(CurrentProgress.CurrentDistanceKm, CurrentProgress.CurrentDrivingTimeTick);
            CurrentProgress.StartNewLeg(newNextStop.IncomingLegDistanceKm, newNextStop.IncomingLegTimeTick);
        }
    }
}
