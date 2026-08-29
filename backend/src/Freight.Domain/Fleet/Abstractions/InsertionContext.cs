using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet.Abstractions;

/// <summary>
/// Everything <see cref="IShipmentInsertionEvaluator.Evaluate"/> needs to judge one
/// hypothetical shipment insertion - the post-insertion route, the truck's capacity, and
/// the driver/timing state to project arrival times against each stop's window.
///
/// The caller builds this by cloning the trip (<see cref="Trip.Clone"/>), running the
/// real <see cref="Trip.AssignShipment"/> on the clone, and passing that clone here. The
/// evaluator walks the clone (marking its stops reached as the projection passes them) -
/// which is why a clone is expected, never the real tracked trip.
/// </summary>
/// <param name="ProposedTrip">The trip clone after the hypothetical insertion has been applied.</param>
/// <param name="TruckCapacity">The truck's total capacity - the running on-board load must never exceed this at any point while walking the route.</param>
/// <param name="Windows">The state the window-feasibility projection needs (driver(s), leg progress, start time, per-stop windows).</param>
public sealed record InsertionContext(
    Trip ProposedTrip,
    Capacity TruckCapacity,
    WindowProjection Windows);

/// <summary>
/// Everything the window-feasibility check needs beyond the proposed stops: how far along
/// its current leg the truck already is, the driver(s) to walk forward, when the walk
/// starts, and the per-stop windows to check the projected arrivals against.
/// </summary>
/// <param name="CurrentLegProgress">The truck's live progress into the nearest Pending stop's leg, if a leg is already in progress; null when the leg is entirely ahead.</param>
/// <param name="Drivers">The driver state the route projection walks forward - single-driver or team.</param>
/// <param name="ProjectionStart">The simulated time the arrival-time projection starts from.</param>
/// <param name="ShipmentWindows">Every Pending stop's own window (a Pickup stop's pickup window, a Delivery stop's delivery window, including the two new stops), keyed by Stop.Id.</param>
public sealed record WindowProjection(
    RouteProgress? CurrentLegProgress,
    DriverProjection Drivers,
    DateTime ProjectionStart,
    IReadOnlyDictionary<Guid, TimeWindow> ShipmentWindows);

/// <summary>
/// The driver state a route projection walks forward. A single-driver truck sets only
/// <see cref="PrimaryLedger"/>/<see cref="PrimaryRules"/>. A team truck also sets the
/// secondary pair and <see cref="ActiveDriverId"/> (which driver is at the wheel at the
/// projection's start); the projection then alternates between the two as their hours
/// require. Ledgers are passed as-is - the projection walks private copies and never
/// mutates them.
/// </summary>
public sealed record DriverProjection(
    DriverComplianceState PrimaryLedger,
    DrivingRules PrimaryRules,
    DriverComplianceState? SecondaryLedger = null,
    DrivingRules? SecondaryRules = null,
    Guid? ActiveDriverId = null)
{
    /// <summary>True when this describes a two-driver (team) truck.</summary>
    public bool IsTeam => SecondaryLedger is not null;

    public static DriverProjection Single(DriverComplianceState ledger, DrivingRules rules) =>
        new(ledger, rules);

    public static DriverProjection Team(
        DriverComplianceState primaryLedger,
        DrivingRules primaryRules,
        DriverComplianceState secondaryLedger,
        DrivingRules secondaryRules,
        Guid activeDriverId) =>
        new(primaryLedger, primaryRules, secondaryLedger, secondaryRules, activeDriverId);
}
