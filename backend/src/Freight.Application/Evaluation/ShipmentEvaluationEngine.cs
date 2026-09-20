using Freight.Application.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;

namespace Freight.Application.Evaluation;

/// <summary>Per-truck result of evaluating one Shipment against one company's fleet.</summary>
public sealed record TruckEvaluationResult(
    Guid TruckId,
    bool IsFeasible,
    int? PickupInsertIndex = null,
    int? DeliveryInsertIndex = null,
    double? AddedDistanceKm = null,
    int? AddedTimeTick = null);

/// <summary>
/// For every Truck belonging to one TruckingCompany, determines whether a given Shipment
/// could feasibly be inserted into that truck's route - and if so, where, and how much
/// extra distance/time the insertion would add. See
/// docs/adr/0012-shipment-evaluation-insertion-search.md for the search design this
/// implements. Called on demand by a dispatcher checking their own fleet, not
/// automatically at booking time, and not run against any other company's trucks.
/// </summary>
public sealed class ShipmentEvaluationEngine(
    IUnitOfWork unitOfWork,
    ShipmentInsertionPlanner insertionPlanner,
    RouteEtaCalculator routeEtaCalculator,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<TruckEvaluationResult>> EvaluateForCompanyAsync(
        Guid shipmentId, Guid truckingCompanyId, CancellationToken cancellationToken = default)
    {
        var shipment = await unitOfWork.Shipments.GetByIdAsync(shipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{shipmentId}' was not found.");

        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(truckingCompanyId, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{truckingCompanyId}' was not found.");

        var companyTrucks = await unitOfWork.Trucks.GetByTruckingCompanyIdAsync(truckingCompanyId, cancellationToken);

        // Resolved once, up front - a truck with no open trip needs a trip-start time for
        // a hypothetical new trip, and that must be the SIMULATION clock's current time,
        // never TimeProvider directly (TimeProvider only seeds the clock the very first
        // time it's created - see SimulationClock.GetOrCreateAsync).
        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var simulatedNow = clock.CurrentTime;

        var results = new List<TruckEvaluationResult>();

        // Every truck is evaluated - no early exit once one is found feasible, since the
        // caller wants the full per-truck picture for their own fleet, not just a yes/no.
        foreach (var truck in companyTrucks)
        {
            if (!PassesPreFilter(truck, shipment))
            {
                results.Add(new TruckEvaluationResult(truck.Id, IsFeasible: false));
                continue;
            }

            // Loaded once per truck and threaded through the whole position search -
            // ShipmentEvaluationEngine.PlanForLoadedAsync skips the redundant re-fetch and
            // re-validation ShipmentInsertionPlanner.PlanAsync(request) would otherwise
            // repeat for every (pickupIndex, deliveryIndex) pair attempted.
            var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);

            results.Add(await EvaluateTruckAsync(truck, shipment, company, trip, simulatedNow, cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// Cheap, no-OSRM gate: type/active/driver preconditions identical to
    /// <see cref="ShipmentInsertionPlanner"/>'s. Deliberately does NOT pre-check capacity -
    /// a truck's load varies by WHERE in its route the shipment would be inserted (pickups
    /// ahead add weight, deliveries ahead remove it), which isn't known until a specific
    /// position is chosen. The real, full-route capacity walk happens inside
    /// <see cref="Freight.Domain.Fleet.Services.IShipmentInsertionEvaluator"/> for each
    /// position actually tried - see docs/adr/0012-shipment-evaluation-insertion-search.md.
    /// </summary>
    private static bool PassesPreFilter(Truck truck, Shipment shipment) =>
        truck.Type == shipment.RequiredTruckType && truck.IsActive && truck.DriverAssignment is not null;

    /// <summary>
    /// Stage 0 (zero OSRM) narrows the pickup-position search using the truck's EXISTING,
    /// already-known route timing; Stage 1/2 (this method, continued) tries real positions
    /// via <see cref="ShipmentInsertionPlanner"/>, nearest-first, stopping at the first
    /// feasible pair. See the ADR's flow diagram for the full picture. <paramref name="trip"/>
    /// is the truck's open Trip, already loaded by the caller (null if it has none).
    /// </summary>
    private async Task<TruckEvaluationResult> EvaluateTruckAsync(
        Truck truck, Shipment shipment, TruckingCompany company, Trip? trip, DateTime simulatedNow, CancellationToken cancellationToken)
    {
        // No open trip yet (or nothing pending on it): nothing to narrow against - the
        // only sensible position is inserting fresh at the front of a new/empty route.
        // Office(return) is excluded from the count - see the comment in
        // FindSearchStartIndexAsync on why insertion indices never count it.
        var pendingStopCount = trip?.Stops.Count(stop => stop.Kind != StopKind.Office && stop.Status == StopStatus.Pending) ?? 0;
        if (trip is null || pendingStopCount == 0)
        {
            return await TryPositionAsync(
                truck, shipment, company, trip, pickupIndex: 0, deliveryIndex: 0, simulatedNow, cancellationToken);
        }

        var startIndex = await FindSearchStartIndexAsync(truck, trip, shipment, cancellationToken);
        if (startIndex is null)
        {
            // Even the last pending stop's projected arrival is already past the pickup
            // window's Latest - every insertion position is monotonically at least that
            // late, so this truck cannot make the window at all.
            return new TruckEvaluationResult(truck.Id, IsFeasible: false);
        }

        for (var pickupIndex = startIndex.Value; pickupIndex <= pendingStopCount; pickupIndex++)
        {
            // Nearest delivery slot first (immediately after pickup), widening outward
            // only on failure - see the ADR for why this order minimizes attempts.
            for (var deliveryIndex = pickupIndex; deliveryIndex <= pendingStopCount; deliveryIndex++)
            {
                var attempt = await TryPositionAsync(
                    truck, shipment, company, trip, pickupIndex, deliveryIndex, simulatedNow, cancellationToken);
                if (attempt.IsFeasible)
                {
                    return attempt;
                }
            }
        }

        return new TruckEvaluationResult(truck.Id, IsFeasible: false);
    }

    /// <summary>
    /// Finds the first pending-stop insertion index worth trying: the slot just before the
    /// first existing pending stop whose projected arrival (using the truck's real,
    /// already-known route timing - <see cref="RouteEtaCalculator"/>, no new OSRM calls)
    /// is on or after the shipment's pickup window opening. Returns null if even the
    /// truck's last pending stop is reached before the window opens.
    /// </summary>
    private async Task<int?> FindSearchStartIndexAsync(Truck truck, Trip trip, Shipment shipment, CancellationToken cancellationToken)
    {
        var assignment = truck.DriverAssignment!;
        var primary = assignment.PrimaryDriver;
        var primaryLedger = primary.ComplianceState
            ?? throw new InvalidOperationException($"Open trip '{trip.Id}' has no compliance ledger for its primary driver.");
        var projectionStart = primaryLedger.LastEvaluatedSimulatedTime;

        var projection = assignment.ConfigurationType == DriverConfigurationType.Team
            ? routeEtaCalculator.CalculateEtasForTeam(
                trip, truck.CurrentProgress,
                primaryLedger, primary.Rules,
                assignment.SecondaryDriver!.ComplianceState!, assignment.SecondaryDriver.Rules,
                assignment.ActiveDriverId ?? primary.Id,
                projectionStart)
            : routeEtaCalculator.CalculateEtas(trip, truck.CurrentProgress, primaryLedger, primary.Rules, projectionStart);

        // Office(return) is excluded - ShipmentInsertionPlanner's insertion indices are
        // only ever counted over non-Office pending stops (see ResolveLegPlanAsync's
        // pendingLocations), so the index numbering here must match exactly.
        var pendingStops = trip.Stops
            .Where(stop => stop.Kind != StopKind.Office && stop.Status == StopStatus.Pending)
            .ToList();

        for (var index = 0; index < pendingStops.Count; index++)
        {
            var arrival = projection.Etas.TryGetValue(pendingStops[index].Id, out var eta) ? eta : projectionStart;
            if (arrival >= shipment.PickupWindow.Earliest)
            {
                return index;
            }
        }

        // Every existing pending stop is reached before the window opens - the only
        // remaining position is appending at the very end.
        var lastArrival = pendingStops.Count > 0 && projection.Etas.TryGetValue(pendingStops[^1].Id, out var lastEta)
            ? lastEta
            : projectionStart;

        return lastArrival <= shipment.PickupWindow.Latest ? pendingStops.Count : null;
    }

    /// <summary>
    /// Tries one (pickupIndex, deliveryIndex) pair for real via
    /// <see cref="ShipmentInsertionPlanner.PlanForLoadedAsync"/> - using the truck/shipment/
    /// company/trip the caller already loaded once for this truck, instead of the id-based
    /// <see cref="ShipmentInsertionPlanner.PlanAsync(AssignShipmentToTruckRequest, CancellationToken)"/>
    /// overload, which would re-fetch and re-validate all four on every attempted position.
    /// On success, reports the added distance/time as the feasible preview's total planned
    /// route figures minus <paramref name="trip"/>'s (the CURRENT, pre-insertion) same
    /// totals - a truck with no open trip yet has nothing to subtract from (added cost
    /// equals the new route's full total).
    /// </summary>
    private async Task<TruckEvaluationResult> TryPositionAsync(
        Truck truck, Shipment shipment, TruckingCompany company, Trip? trip,
        int pickupIndex, int deliveryIndex, DateTime simulatedNow, CancellationToken cancellationToken)
    {
        try
        {
            // tripStartTime only matters when trip is null (a hypothetical new trip) -
            // PlanForLoadedAsync ignores it otherwise. Must be the simulation clock's
            // current time, not wall-clock time - see EvaluateForCompanyAsync's comment.
            var prepared = await insertionPlanner.PlanForLoadedAsync(
                truck, shipment, company, trip, pickupIndex, deliveryIndex, simulatedNow, cancellationToken);
            if (!prepared.Feasibility.IsFeasible)
            {
                return new TruckEvaluationResult(truck.Id, IsFeasible: false);
            }

            var addedDistanceKm = prepared.Preview.TotalPlannedDistanceKm - (trip?.TotalPlannedDistanceKm ?? 0);
            var addedTimeTick = prepared.Preview.TotalPlannedTimeTick - (trip?.TotalPlannedTimeTick ?? 0);

            return new TruckEvaluationResult(
                truck.Id, IsFeasible: true, pickupIndex, deliveryIndex, addedDistanceKm, addedTimeTick);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Trip.AssignShipment rejects an out-of-range insertion index (e.g. delivery
            // beyond the route's current end) - just means this position doesn't exist,
            // not a real error.
            return new TruckEvaluationResult(truck.Id, IsFeasible: false);
        }
    }
}
