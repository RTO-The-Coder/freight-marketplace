using Freight.Domain.Fleet.Enums;
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
    /// Whether the truck may accept shipments at all - an administrative flag, separate
    /// from the derived operational <see cref="Status"/>. Always false without a company.
    /// </summary>
    public bool IsActive { get; private set; }

    public TruckType Type { get; private set; }
    public TruckSize Size { get; private set; }

    /// <summary>Derived from <see cref="TruckSize"/> at creation - never entered independently.</summary>
    public Capacity Capacity { get; private set; } = null!;

    public DriverAssignment? DriverAssignment { get; private set; }
    public bool HazmatCertified { get; private set; }

    /// <summary>
    /// Progress along the current route leg, in fixed 5-minute ticks. Null until the first
    /// leg starts - set by <see cref="SyncProgressToNextStop"/> after <see cref="Trip.AssignShipment"/>.
    /// </summary>
    public RouteProgress? CurrentProgress { get; private set; }

    /// <summary>
    /// Derived operational state - never set directly. This trip-less shortcut can only
    /// return Idle/AtOffice; callers with the open trip should call
    /// <see cref="DetermineStatus(Trip?)"/> (or the wait-aware overload) instead.
    /// </summary>
    public TruckStatus Status => DetermineStatus();

    // EF Core materializer only - Create(...) is the sole construction path for app code.
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

    /// <summary>
    /// Marks the truck able to accept shipments. Requires a company and at least a primary
    /// driver; losing either later forces <see cref="IsActive"/> back to false.
    /// </summary>
    public void Activate()
    {
        if (TruckingCompanyId is null)
        {
            throw new InvalidOperationException("A truck cannot be activated before it is assigned to a trucking company.");
        }

        if (DriverAssignment is null)
        {
            throw new InvalidOperationException("A truck cannot be activated before at least a primary driver is assigned to it.");
        }

        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Assigns one or two drivers. A secondary driver is only permitted on
    /// <see cref="TruckSize.Large"/> trucks. Replaces any existing assignment with a fresh
    /// one whose active-driver pointer starts on the (new) primary driver.
    /// </summary>
    public void AssignDrivers(Driver primaryDriver, Driver? secondaryDriver = null)
    {
        ArgumentNullException.ThrowIfNull(primaryDriver);

        DriverAssignment = secondaryDriver is null
            ? DriverAssignment.Single(primaryDriver)
            : DriverAssignment.Team(primaryDriver, secondaryDriver, Size);
    }

    /// <summary>
    /// Clears the driver assignment and forces <see cref="IsActive"/> false. The open-trip
    /// guard (a moving truck cannot lose its driver) lives in the handler, not here.
    /// </summary>
    public void RemoveDrivers()
    {
        DriverAssignment = null;
        IsActive = false;
    }

    /// <summary>
    /// Resets every assigned driver's compliance ledger to fully-rested, anchored at
    /// <paramref name="tripStartedAt"/> - called when the truck opens a fresh trip.
    /// </summary>
    public void BeginTripCompliance(DateTime tripStartedAt)
    {
        if (DriverAssignment is null)
        {
            throw new InvalidOperationException("Cannot begin trip compliance before drivers are assigned to this truck.");
        }

        DriverAssignment.PrimaryDriver.ResetComplianceForNewTrip(tripStartedAt);
        DriverAssignment.SecondaryDriver?.ResetComplianceForNewTrip(tripStartedAt);
    }

    /// <summary>
    /// Sets which assigned driver is at the wheel - the one-directional stickiness
    /// invariant is enforced by <see cref="Fleet.DriverAssignment"/>.
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
    /// Operational status: Idle with no driver assignment, AtOffice with no open trip or
    /// heading back to the office, else Running. Never returns
    /// <see cref="TruckStatus.Parked"/> - use the three-argument overload for that.
    /// (The Idle branch also tests <c>DriverAssignment.HasDriverAbleToDrive</c>, hardcoded
    /// true today, so a null assignment is currently the only path to Idle.)
    /// </summary>
    public TruckStatus DetermineStatus(Trip? currentTrip = null) =>
        DetermineStatus(currentTrip, simulatedNow: null, nextStopWindow: null);

    /// <summary>
    /// Wait-aware overload: also returns <see cref="TruckStatus.Parked"/> when the current
    /// leg is complete but <paramref name="nextStopWindow"/> has not opened at
    /// <paramref name="simulatedNow"/>. A null clock or window reports Running instead.
    /// </summary>
    public TruckStatus DetermineStatus(Trip? currentTrip, DateTime? simulatedNow, TimeWindow? nextStopWindow)
    {
        if (DriverAssignment is null || !DriverAssignment.HasDriverAbleToDrive)
        {
            return TruckStatus.Idle;
        }

        if (currentTrip is null || currentTrip.IsAtOffice)
        {
            return TruckStatus.AtOffice;
        }

        if (simulatedNow is { } now
            && nextStopWindow is not null
            && CurrentProgress is { } progress
            && progress.IsLegComplete()
            && now < nextStopWindow.Earliest)
        {
            return TruckStatus.Parked;
        }

        return TruckStatus.Running;
    }

    /// <summary>
    /// Updates <see cref="CurrentProgress"/> to the trip's current next stop after an
    /// insertion. If the next stop changed while the truck was mid-leg, the partial leg is
    /// banked onto the trip (<see cref="Trip.BankPartialLeg"/>) and a fresh leg started;
    /// otherwise CurrentProgress is left untouched. <paramref name="previousNextStopId"/>
    /// is the next-stop id from before the insertion.
    /// </summary>
    public void SyncProgressToNextStop(Trip trip, Guid? previousNextStopId)
    {
        ArgumentNullException.ThrowIfNull(trip);

        if (trip.TruckId != Id)
        {
            throw new ArgumentException("This trip does not belong to this truck.", nameof(trip));
        }

        var newNextStop = trip.NextStop
            ?? throw new InvalidOperationException("Trip has no pending stop to sync progress to.");

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
