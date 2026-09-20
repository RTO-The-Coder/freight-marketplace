using Freight.Domain.Client;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Fleet;

/// <summary>
/// Shared load -> measure-legs -> preview-on-clone -> evaluate flow behind both
/// <see cref="AssignShipmentToTruckHandler"/> (a single caller-chosen truck/position) and
/// the shipment-matching engine (many candidate trucks/positions) - see
/// docs/adr/0012-shipment-matching-insertion-search.md. Mutates nothing that survives the
/// call; returns the feasibility verdict plus the (possibly freshly-opened, uncommitted)
/// real aggregates for the caller to commit or discard.
/// </summary>
public sealed class ShipmentInsertionPlanner(
    IUnitOfWork unitOfWork,
    IShipmentInsertionEvaluator insertionEvaluator,
    IRoutingService routingService,
    TimeProvider timeProvider)
{
    public async Task<PreparedInsertion> PlanAsync(
        AssignShipmentToTruckRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var shipment = await unitOfWork.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{request.ShipmentId}' was not found.");

        if (truck.TruckingCompanyId is null)
        {
            throw new InvalidOperationException("Truck must belong to a trucking company before it can accept a shipment.");
        }

        if (!truck.IsActive)
        {
            throw new InvalidOperationException("Truck must be active to accept a shipment.");
        }

        if (truck.Type != shipment.RequiredTruckType)
        {
            throw new InvalidOperationException(
                $"Truck type mismatch: this shipment requires a {shipment.RequiredTruckType} truck, but {truck.TruckName} is a {truck.Type}.");
        }

        if (truck.DriverAssignment is null)
        {
            throw new InvalidOperationException("Truck must have a driver assigned before it can accept a shipment.");
        }

        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(truck.TruckingCompanyId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{truck.TruckingCompanyId.Value}' was not found.");

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);

        DateTime? tripStartTime = null;
        if (trip is null)
        {
            // TripStartTime only applies when a fresh trip is opened - the dispatcher's
            // planned departure, which may be later than "now" to line up with a pickup
            // window. Adding to an existing trip keeps that trip's own StartedAt.
            var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
                () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            tripStartTime = request.TripStartTime ?? clock.CurrentTime;
        }

        return await PlanForLoadedAsync(
            truck, shipment, company, trip, request.PickupInsertIndex, request.DeliveryInsertIndex,
            tripStartTime, cancellationToken);
    }

    /// <summary>
    /// Same feasibility check as <see cref="PlanAsync(AssignShipmentToTruckRequest, CancellationToken)"/>,
    /// for a caller that already has the Truck/Shipment/TruckingCompany/open-Trip loaded
    /// (e.g. evaluating many candidate positions for the same truck in a row) - skips the
    /// redundant re-fetch and re-validation that overload does per call. Callers are
    /// responsible for the same preconditions that overload enforces (company/active/
    /// type-match/driver-assignment) - this entry point does not re-check them.
    /// </summary>
    public async Task<PreparedInsertion> PlanForLoadedAsync(
        Truck truck,
        Shipment shipment,
        TruckingCompany company,
        Trip? trip,
        int pickupInsertIndex,
        int deliveryInsertIndex,
        DateTime? tripStartTime,
        CancellationToken cancellationToken = default)
    {
        var isNewTrip = trip is null;
        trip ??= Trip.Open(truck.Id, company.Id, tripStartTime ?? timeProvider.GetUtcNow().UtcDateTime);

        // Insertion-index bounds are validated inside Trip.AssignShipment against the
        // pending-stop list - no need to re-check here.

        // Fresh GeoLocation/Capacity instances, not the tracked ones off Shipment/
        // TruckingCompany - EF Core's change tracker treats owned-type instances by
        // reference identity, so reusing shipment.PickupLocation etc. directly here
        // makes it conflate Stop's copy with Shipment's/TruckingCompany's owned
        // navigation of the same CLR type, corrupting insert/update detection for the
        // newly-created Stop rows (they get emitted as UPDATEs instead of INSERTs,
        // affecting 0 rows and throwing DbUpdateConcurrencyException).
        var pickupLocation = GeoLocation.Create(shipment.PickupLocation.Latitude, shipment.PickupLocation.Longitude);
        var deliveryLocation = GeoLocation.Create(shipment.DeliveryLocation.Latitude, shipment.DeliveryLocation.Longitude);
        var officeLocation = GeoLocation.Create(company.OfficeLocation.Latitude, company.OfficeLocation.Longitude);

        var shipmentSize = Capacity.Create(shipment.Load.WeightKg, shipment.Load.VolumeCubicMeters);

        // Measure every road leg the insertion creates BEFORE previewing it, so the same
        // measured figures feed both the feasibility preview and the real insertion (one
        // set of OSRM calls, not two). An unroutable/unreachable leg aborts here -
        // Phase 1 has no fallback distance source (ADR 0011).
        var legPlan = await ResolveLegPlanAsync(
            trip, truck, company, pickupLocation, deliveryLocation, officeLocation,
            pickupInsertIndex, deliveryInsertIndex, isNewTrip, cancellationToken);

        // Preview the insertion on a throwaway clone so feasibility runs against the
        // route as it WOULD look, without mutating the real trip.
        var preview = trip.Clone();
        preview.AssignShipment(
            shipment.Id, shipmentSize, pickupLocation, deliveryLocation, officeLocation,
            pickupInsertIndex, deliveryInsertIndex, legPlan);

        var windows = await BuildShipmentWindowsAsync(preview, shipment, cancellationToken);
        var windowProjection = BuildWindowProjection(truck, trip, isNewTrip, windows);

        var feasibility = insertionEvaluator.Evaluate(
            new InsertionContext(preview, truck.Capacity, windowProjection));

        var stopInputs = new StopInputs(shipmentSize, pickupLocation, deliveryLocation, officeLocation);
        return new PreparedInsertion(feasibility, truck, shipment, trip, preview, isNewTrip, legPlan, stopInputs);
    }

    /// <summary>
    /// Measures, via <see cref="IRoutingService"/>, every road leg
    /// <see cref="Trip.AssignShipment"/> will write for this insertion:
    /// <list type="bullet">
    ///   <item>pickup's incoming leg - from its predecessor (a pending stop, the company
    ///     office for a route that starts here, or the truck's live mid-leg position when
    ///     the pickup is inserted ahead of a moving truck) to the pickup;</item>
    ///   <item>the rewritten incoming leg of whatever stop then follows the pickup;</item>
    ///   <item>the same pair for the delivery;</item>
    ///   <item>the Office(return) leg, used only when this is the trip's first shipment.</item>
    /// </list>
    /// </summary>
    private async Task<LegPlan> ResolveLegPlanAsync(
        Trip trip,
        Truck truck,
        TruckingCompany company,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        GeoLocation officeLocation,
        int pickupInsertIndex,
        int deliveryInsertIndex,
        bool isNewTrip,
        CancellationToken cancellationToken)
    {
        var pendingLocations = trip.Stops
            .Where(stop => stop.Kind != StopKind.Office && stop.Status == StopStatus.Pending)
            .Select(stop => stop.Location)
            .ToList();

        // Build the pending non-office route exactly as it will look after both stops are
        // spliced in - pickup at pickupInsertIndex, then delivery at deliveryInsertIndex + 1
        // (the +1 accounts for the pickup that now sits before it). The element before
        // index 0 is the route's start point: the company office, or the truck's live
        // position when it is already driving.
        var routeStart = RouteStartLocation(trip, truck, company, isNewTrip);

        var finalRoute = new List<GeoLocation>(pendingLocations);
        finalRoute.Insert(pickupInsertIndex, pickupLocation);
        var deliveryFinalIndex = deliveryInsertIndex + 1;
        finalRoute.Insert(deliveryFinalIndex, deliveryLocation);

        var pickupFinalIndex = pickupInsertIndex;

        GeoLocation Before(int index) => index == 0 ? routeStart : finalRoute[index - 1];

        // Each inserted stop's incoming leg is the hop from whatever now precedes it.
        var pickupIncoming = await GetLegAsync(Before(pickupFinalIndex), pickupLocation, cancellationToken);
        var deliveryIncoming = await GetLegAsync(Before(deliveryFinalIndex), deliveryLocation, cancellationToken);

        // Trip.AssignShipment inserts the pickup against the PRE-insertion pending list,
        // then the delivery against the list-with-pickup. Each insertion rewrites the
        // incoming leg of the existing stop it landed in front of - matched here index for
        // index. A null follower means the stop was appended at the end (the Office return
        // leg below covers that hop instead).
        RouteSegment? pickupToFollower = pickupInsertIndex < pendingLocations.Count
            ? await GetLegAsync(pickupLocation, pendingLocations[pickupInsertIndex], cancellationToken)
            : null;

        var pendingWithPickup = new List<GeoLocation>(pendingLocations);
        pendingWithPickup.Insert(pickupInsertIndex, pickupLocation);
        RouteSegment? deliveryToFollower = deliveryFinalIndex < pendingWithPickup.Count
            ? await GetLegAsync(deliveryLocation, pendingWithPickup[deliveryFinalIndex], cancellationToken)
            : null;

        // The Office(return) leg is only written the first time this trip receives a
        // shipment; EnsureOfficeStop ignores it once an Office stop exists.
        RouteSegment toOffice;
        if (trip.Stops.Any(stop => stop.Kind == StopKind.Office))
        {
            toOffice = new RouteSegment(0, 0);
        }
        else
        {
            toOffice = await GetLegAsync(finalRoute[^1], officeLocation, cancellationToken);
        }

        return new LegPlan(pickupIncoming, pickupToFollower, deliveryIncoming, deliveryToFollower, toOffice);
    }

    /// <summary>
    /// Where the truck sets off from for a leg that has no preceding stop: the company
    /// office for a route whose first pending stop is the insertion, or - when a pickup
    /// is inserted ahead of a truck already driving a leg - the truck's live position,
    /// interpolated along its current leg (distance tracks time, see
    /// <see cref="RouteProgress"/>).
    /// </summary>
    private static GeoLocation RouteStartLocation(Trip trip, Truck truck, TruckingCompany company, bool isNewTrip)
    {
        if (isNewTrip || truck.CurrentProgress is null)
        {
            return GeoLocation.Create(company.OfficeLocation.Latitude, company.OfficeLocation.Longitude);
        }

        var nextStop = trip.NextStop;
        if (nextStop is null)
        {
            return GeoLocation.Create(company.OfficeLocation.Latitude, company.OfficeLocation.Longitude);
        }

        var lastReached = trip.Stops.LastOrDefault(stop => stop.Status == StopStatus.Reached);
        var legStart = lastReached?.Location
            ?? GeoLocation.Create(company.OfficeLocation.Latitude, company.OfficeLocation.Longitude);

        return legStart.InterpolateTo(nextStop.Location, truck.CurrentProgress.GetProgressFraction());
    }

    private async Task<RouteSegment> GetLegAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken)
    {
        var leg = await routingService.GetRouteAsync(from, to, cancellationToken);
        return new RouteSegment(leg.DistanceKm, leg.TimeTicks);
    }

    /// <summary>
    /// Assembles the driver + timing state the feasibility projection walks forward. A
    /// fresh trip projects from a fully-rested ledger per driver anchored at the planned
    /// departure, no leg in progress; an existing trip projects from where its ledger(s)
    /// currently stand, carrying live leg progress. A team supplies both ledgers + the
    /// active-driver pointer.
    /// </summary>
    private static WindowProjection BuildWindowProjection(
        Truck truck, Trip trip, bool isNewTrip, IReadOnlyDictionary<Guid, TimeWindow> windows)
    {
        var assignment = truck.DriverAssignment
            ?? throw new InvalidOperationException($"Truck '{truck.Id}' has no driver assignment.");

        var primary = assignment.PrimaryDriver;
        var secondary = assignment.SecondaryDriver;

        DateTime projectionStart;
        RouteProgress? currentLegProgress;
        DriverComplianceState primaryLedger;
        DriverComplianceState? secondaryLedger;

        if (isNewTrip)
        {
            projectionStart = trip.StartedAt;
            currentLegProgress = null;
            primaryLedger = new DriverComplianceState(primary.Id, trip.StartedAt);
            secondaryLedger = secondary is null ? null : new DriverComplianceState(secondary.Id, trip.StartedAt);
        }
        else
        {
            primaryLedger = primary.ComplianceState
                ?? throw new InvalidOperationException($"Open trip '{trip.Id}' has no compliance ledger for its primary driver.");
            // Both ledgers share LastEvaluatedSimulatedTime (EvaluateTeam sets both), so
            // the primary's is the projection start for a team too.
            projectionStart = primaryLedger.LastEvaluatedSimulatedTime;
            currentLegProgress = truck.CurrentProgress;
            secondaryLedger = secondary is null
                ? null
                : secondary.ComplianceState
                    ?? throw new InvalidOperationException($"Open trip '{trip.Id}' has no compliance ledger for its secondary driver.");
        }

        var drivers = secondary is null || secondaryLedger is null
            ? DriverProjection.Single(primaryLedger, primary.Rules)
            : DriverProjection.Team(
                primaryLedger, primary.Rules,
                secondaryLedger, secondary.Rules,
                assignment.ActiveDriverId ?? primary.Id);

        return new WindowProjection(currentLegProgress, drivers, projectionStart, windows);
    }

    /// <summary>
    /// Each Pending Pickup/Delivery stop's own window, keyed by stop id - the new
    /// shipment's from <paramref name="newShipment"/>, every other from its own Shipment.
    /// </summary>
    private async Task<Dictionary<Guid, TimeWindow>> BuildShipmentWindowsAsync(
        Trip preview, Shipment newShipment, CancellationToken cancellationToken)
    {
        var windows = new Dictionary<Guid, TimeWindow>();

        foreach (var stop in preview.Stops)
        {
            if (stop.Status != StopStatus.Pending || stop.Kind == StopKind.Office || stop.ShipmentId is not { } shipmentId)
            {
                continue;
            }

            if (shipmentId == newShipment.Id)
            {
                windows[stop.Id] = stop.Kind == StopKind.Pickup ? newShipment.PickupWindow : newShipment.DeliveryWindow;
                continue;
            }

            var existingShipment = await unitOfWork.Shipments.GetByIdAsync(shipmentId, cancellationToken)
                ?? throw new InvalidOperationException($"Shipment '{shipmentId}' referenced by stop '{stop.Id}' was not found.");

            windows[stop.Id] = stop.Kind == StopKind.Pickup ? existingShipment.PickupWindow : existingShipment.DeliveryWindow;
        }

        return windows;
    }
}

/// <summary>
/// Output of <see cref="ShipmentInsertionPlanner.PlanAsync"/> - see that method.
/// <paramref name="Trip"/> is the real, PRE-insertion trip (what a commit call mutates);
/// <paramref name="Preview"/> is the throwaway POST-insertion clone the feasibility check
/// ran against - read its totals (e.g. <see cref="Freight.Domain.Fleet.Trip.TotalPlannedDistanceKm"/>)
/// for "what would the route look like" figures, never <paramref name="Trip"/>'s.
/// </summary>
public sealed record PreparedInsertion(
    InsertionFeasibility Feasibility,
    Truck Truck,
    Shipment Shipment,
    Trip Trip,
    Trip Preview,
    bool IsNewTrip,
    LegPlan LegPlan,
    StopInputs StopInputs);

/// <summary>
/// Fresh (non-tracked) owned-type instances for the real <see cref="Trip.AssignShipment"/>
/// call - built once and reused so EF's change tracker never conflates them with
/// Shipment's / TruckingCompany's own navigations (see the fresh-instance comment in
/// <see cref="ShipmentInsertionPlanner.PlanAsync"/>).
/// </summary>
public sealed record StopInputs(
    Capacity ShipmentSize,
    GeoLocation PickupLocation,
    GeoLocation DeliveryLocation,
    GeoLocation OfficeLocation);
