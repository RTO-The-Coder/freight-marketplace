using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet.Abstractions;

/// <summary>
/// Everything <see cref="IShipmentInsertionEvaluator.Evaluate"/> needs to judge one
/// hypothetical shipment insertion - the post-insertion route plus the driver and timing
/// state required to project arrival times against each stop's window.
///
/// The caller builds this by cloning the trip (<see cref="Trip.Clone"/>), running the
/// real <see cref="Trip.AssignShipment"/> on the clone, and passing that clone here
/// alongside the truck's live progress, the active driver's ledger/rules, and a window
/// lookup covering every Pending stop's shipment. The evaluator walks the clone (marking
/// its stops reached as the projection passes them) - which is why a clone is expected,
/// never the real tracked trip.
/// </summary>
/// <param name="ProposedTrip">The trip clone after the hypothetical insertion has been applied.</param>
/// <param name="TruckCapacity">The truck's total capacity - the running on-board load must never exceed this at any point while walking the route.</param>
/// <param name="CurrentLegProgress">The truck's live progress into the nearest Pending stop's leg, if a leg is already in progress; null when the leg is entirely ahead.</param>
/// <param name="DriverLedger">The active driver's compliance ledger - passed as-is; the evaluator projects on a private copy and never mutates it.</param>
/// <param name="DriverRules">The active driver's fixed driving-rule variant.</param>
/// <param name="ProjectionStart">The simulated time the arrival-time projection starts from.</param>
/// <param name="ShipmentWindows">Every Pending stop's own window (a Pickup stop's pickup window, a Delivery stop's delivery window, including the two new stops), keyed by Stop.Id.</param>
public sealed record InsertionContext(
    Trip ProposedTrip,
    Capacity TruckCapacity,
    RouteProgress? CurrentLegProgress,
    DriverComplianceState DriverLedger,
    DrivingRules DriverRules,
    DateTime ProjectionStart,
    IReadOnlyDictionary<Guid, TimeWindow> ShipmentWindows);
