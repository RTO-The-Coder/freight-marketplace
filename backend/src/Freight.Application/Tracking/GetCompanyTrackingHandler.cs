using Freight.Application.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking.Enums;

namespace Freight.Application.Tracking;

public sealed record GetCompanyTrackingRequest(Guid TruckingCompanyId);

/// <summary>What a running trip's truck is doing right now - the "Now" line of its card.</summary>
public enum TrackingNowKind
{
    /// <summary>The trip's planned start is still ahead; <see cref="TrackingNowDto.Until"/> is the start.</summary>
    NotStarted,

    Driving,

    /// <summary>Parked at <see cref="TrackingNowDto.ToPlace"/> until its window opens (<see cref="TrackingNowDto.Until"/>).</summary>
    WaitingForWindow,

    OnBreak,
    OnDailyRest,
    OnWeeklyRest,
}

/// <summary>
/// The "Now" line: the truck is between <see cref="FromPlace"/> (the last stop it reached,
/// or its office) and <see cref="ToPlace"/> (its next stop). <see cref="KmLeft"/> is the
/// distance still to drive on that leg; <see cref="DriverName"/> who is at the wheel;
/// <see cref="Until"/> when a not-started trip departs, a wait ends or a rest ends.
/// </summary>
public sealed record TrackingNowDto(
    TrackingNowKind Kind,
    string FromPlace,
    string? ToPlace,
    double KmLeft,
    string? DriverName,
    DateTime? Until);

/// <summary>A trip stop with its place name, for the stop progress line and the map. <see cref="ProjectedArrival"/> is set for Pending stops only.</summary>
public sealed record TrackingStopDto(
    Guid StopId,
    Guid? ShipmentId,
    StopKind Kind,
    StopStatus Status,
    int Sequence,
    double Latitude,
    double Longitude,
    string PlaceName,
    double IncomingLegDistanceKm,
    int IncomingLegTimeTick,
    DateTime? ReachedAt,
    DateTime? ProjectedArrival);

/// <summary>
/// One of the company's offers for a truck, still in play: accepted by the shipper but not
/// yet added to the trip, or sent and waiting for the shipper's answer until <see cref="WaitingUntil"/>.
/// </summary>
public sealed record TrackingOfferDto(
    Guid OfferId,
    Guid ShipmentId,
    ShipmentOfferStatus Status,
    string FromPlace,
    string ToPlace,
    string ShipperName,
    double LoadWeightKg,
    double LoadVolumeCubicMeters,
    DateTime PickupWindowEarliest,
    DateTime PickupWindowLatest,
    DateTime DeliveryWindowEarliest,
    DateTime DeliveryWindowLatest,
    decimal PriceEur,
    DateTime? WaitingUntil);

/// <summary>
/// A running trip as one Tracking card. Distance and driving include the leg the truck is
/// on now; <see cref="RestTicksSoFar"/> counts breaks and daily/weekly rests.
/// <see cref="Position"/> is where the truck is now.
/// </summary>
public sealed record TrackingTripDto(
    Guid TripId,
    Guid TruckId,
    string TruckName,
    TruckType TruckType,
    TruckSize TruckSize,
    IReadOnlyList<TrackingDriverDto> Drivers,
    DateTime StartedAt,
    DateTime? OfficeEta,
    double DistanceKmSoFar,
    int DrivingTicksSoFar,
    int RestTicksSoFar,
    TrackingNowDto Now,
    TruckPositionDto Position,
    int StopsDone,
    IReadOnlyList<TrackingStopDto> Stops,
    IReadOnlyList<TrackedShipmentDto> Shipments,
    IReadOnlyList<TrackingOfferDto> Offers);

/// <summary>An idle truck (no running trip) with an offer the shipper accepted - "Ready to start".</summary>
public sealed record ReadyTruckDto(
    Guid TruckId,
    string TruckName,
    TruckType TruckType,
    TruckSize TruckSize,
    IReadOnlyList<TrackingDriverDto> Drivers,
    IReadOnlyList<TrackingOfferDto> AcceptedOffers);

public sealed record CompanyTrackingResponse(
    DateTime Now,
    string OfficePlaceName,
    IReadOnlyList<TrackingTripDto> Trips,
    IReadOnlyList<ReadyTruckDto> ReadyTrucks);

/// <summary>
/// The company's Tracking page in one call: every running trip with its drivers, totals,
/// "Now" line, stops, shipments (delivered, on board, to pick) and its truck's offers still in
/// play, plus the idle trucks with an accepted offer. Rejected offers and offers whose limit or
/// deadline has passed are left out. Arrival times come from <see cref="TruckEtaProjector"/>.
/// </summary>
public sealed class GetCompanyTrackingHandler(IUnitOfWork unitOfWork, RouteEtaCalculator routeEtaCalculator, TimeProvider timeProvider)
{
    private readonly TruckEtaProjector projector = new(routeEtaCalculator);

    public async Task<CompanyTrackingResponse> GetTrackingAsync(GetCompanyTrackingRequest request, CancellationToken cancellationToken = default)
    {
        var companyId = request.TruckingCompanyId;
        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(companyId, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{companyId}' was not found.");

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var now = clock.CurrentTime;

        var trucks = (await unitOfWork.Trucks.GetByTruckingCompanyIdAsync(companyId, cancellationToken))
            .ToDictionary(truck => truck.Id);
        var trips = await unitOfWork.Trips.GetOpenTripsByCompanyAsync(companyId, cancellationToken);
        var offers = await unitOfWork.ShipmentOffers.GetByCompanyIdAsync(companyId, cancellationToken);

        var shipmentIds = trips
            .SelectMany(trip => trip.Stops)
            .Where(stop => stop.ShipmentId is not null)
            .Select(stop => stop.ShipmentId!.Value)
            .Concat(offers.Select(offer => offer.ShipmentId))
            .ToHashSet();
        var shipments = (await unitOfWork.Shipments.GetByIdsAsync(shipmentIds, cancellationToken))
            .ToDictionary(shipment => shipment.Id);
        var shipperNames = await TrackedShipments.ShipperNamesAsync(unitOfWork, shipments.Values, cancellationToken);
        var prices = TrackedShipments.Prices(offers);

        var liveOffers = LiveOffersByTruck(offers, shipments, shipperNames, companyId, now);

        var tripDtos = new List<TrackingTripDto>();
        foreach (var trip in trips)
        {
            if (!trucks.TryGetValue(trip.TruckId, out var truck))
            {
                continue;
            }

            tripDtos.Add(BuildTrip(trip, truck, company, now, shipments, shipperNames, prices,
                liveOffers.GetValueOrDefault(truck.Id) ?? []));
        }

        var trucksOnTrips = trips.Select(trip => trip.TruckId).ToHashSet();
        var ready = trucks.Values
            .Where(truck => !trucksOnTrips.Contains(truck.Id))
            .Select(truck => (truck, accepted: (liveOffers.GetValueOrDefault(truck.Id) ?? [])
                .Where(offer => offer.Status == ShipmentOfferStatus.Accepted)
                .ToList()))
            .Where(pair => pair.accepted.Count > 0)
            .OrderBy(pair => pair.truck.TruckName)
            .Select(pair => new ReadyTruckDto(
                pair.truck.Id, pair.truck.TruckName, pair.truck.Type, pair.truck.Size,
                TrackedShipments.Drivers(pair.truck), pair.accepted))
            .ToList();

        return new CompanyTrackingResponse(now, company.OfficePlaceName, tripDtos, ready);
    }

    private TrackingTripDto BuildTrip(
        Trip trip,
        Truck truck,
        TruckingCompany company,
        DateTime now,
        IReadOnlyDictionary<Guid, Shipment> shipments,
        IReadOnlyDictionary<Guid, string> shipperNames,
        IReadOnlyDictionary<Guid, decimal> prices,
        IReadOnlyList<TrackingOfferDto> offers)
    {
        TruckTripProjection? projection = null;
        try
        {
            projection = projector.Project(truck, trip, TruckEtaProjector.StopWindows(trip, shipments));
        }
        catch (InvalidOperationException)
        {
            // A truck the simulation cannot project (e.g. a driver without a ledger) still
            // shows its card - just without arrival times.
        }

        var etas = projection?.Etas ?? new Dictionary<Guid, DateTime>();
        var placeNames = trip.Stops.ToDictionary(stop => stop.Id, stop => PlaceName(stop, shipments, company));

        var stops = trip.Stops
            .Select(stop => new TrackingStopDto(
                stop.Id, stop.ShipmentId, stop.Kind, stop.Status, stop.Sequence,
                stop.Location.Latitude, stop.Location.Longitude, placeNames[stop.Id],
                stop.IncomingLegDistanceKm, stop.IncomingLegTimeTick, stop.ReachedAt,
                etas.TryGetValue(stop.Id, out var eta) ? eta : null))
            .ToList();

        var office = trip.Stops.FirstOrDefault(stop => stop.Kind == StopKind.Office);
        var officeEta = office is not null && etas.TryGetValue(office.Id, out var oe) ? oe : (DateTime?)null;

        var progress = truck.CurrentProgress;
        var location = TruckPositionLocator.Locate(truck, trip, company.OfficeLocation);
        var position = new TruckPositionDto(
            truck.Id, trip.Id, location.Position.Latitude, location.Position.Longitude,
            location.HeadingToStopId, location.LegProgressFraction);

        var drivers = trip.Drivers.Count > 0 ? TrackedShipments.Drivers(trip) : TrackedShipments.Drivers(truck);

        return new TrackingTripDto(
            trip.Id,
            truck.Id,
            truck.TruckName,
            truck.Type,
            truck.Size,
            drivers,
            trip.StartedAt,
            officeEta,
            trip.DistanceTravelledSoFar + (progress?.CurrentDistanceKm ?? 0),
            trip.TimeElapsedSoFar + (progress?.CurrentDrivingTimeTick ?? 0),
            trip.RestTimeSoFar,
            Now(trip, truck, company, now, projection, placeNames),
            position,
            trip.Stops.Count(stop => stop.Status == StopStatus.Reached),
            stops,
            TrackedShipments.Build(trip, shipments, shipperNames, prices, etas),
            offers);
    }

    private static TrackingNowDto Now(
        Trip trip,
        Truck truck,
        TruckingCompany company,
        DateTime now,
        TruckTripProjection? projection,
        IReadOnlyDictionary<Guid, string> placeNames)
    {
        var lastReached = trip.Stops.LastOrDefault(stop => stop.Status == StopStatus.Reached);
        var from = lastReached is not null ? placeNames[lastReached.Id] : company.OfficePlaceName;
        var next = trip.NextStop;
        var to = next is not null ? placeNames[next.Id] : null;
        var progress = truck.CurrentProgress;
        var kmLeft = progress is null ? next?.IncomingLegDistanceKm ?? 0 : progress.TotalDistanceKm - progress.CurrentDistanceKm;

        var assignment = truck.DriverAssignment;
        var activeDriver = assignment is null ? null
            : assignment.SecondaryDriver is { } secondary && assignment.ActiveDriverId == secondary.Id ? secondary
            : assignment.PrimaryDriver;
        var driverName = activeDriver is null ? null : $"{activeDriver.FirstName} {activeDriver.LastName}";

        if (trip.StartedAt >= now)
        {
            return new TrackingNowDto(TrackingNowKind.NotStarted, from, to, kmLeft, driverName, trip.StartedAt);
        }

        if (projection?.Waiting is { } waiting)
        {
            return new TrackingNowDto(TrackingNowKind.WaitingForWindow, from, to, 0, driverName, waiting.WindowOpensAt);
        }

        if (activeDriver?.ComplianceState is { MinutesRemainingInCurrentActivity: > 0 } ledger)
        {
            var restKind = ledger.CurrentActivity switch
            {
                DriverActivity.OnBreak => TrackingNowKind.OnBreak,
                DriverActivity.OnDailyRest => TrackingNowKind.OnDailyRest,
                DriverActivity.OnWeeklyRest => TrackingNowKind.OnWeeklyRest,
                _ => (TrackingNowKind?)null,
            };

            if (restKind is { } kind)
            {
                return new TrackingNowDto(kind, from, to, kmLeft, driverName,
                    ledger.LastEvaluatedSimulatedTime.AddMinutes(ledger.MinutesRemainingInCurrentActivity));
            }
        }

        return new TrackingNowDto(TrackingNowKind.Driving, from, to, kmLeft, driverName, null);
    }

    /// <summary>
    /// Per truck, the company's offers still in play: accepted by the shipper and not yet on
    /// a trip (the shipment is still Pending and booked to this company), or Pending and still
    /// waiting for the shipper. Rejected and expired offers are dropped.
    /// </summary>
    private static Dictionary<Guid, List<TrackingOfferDto>> LiveOffersByTruck(
        IEnumerable<ShipmentOffer> offers,
        IReadOnlyDictionary<Guid, Shipment> shipments,
        IReadOnlyDictionary<Guid, string> shipperNames,
        Guid companyId,
        DateTime now)
    {
        var byTruck = new Dictionary<Guid, List<TrackingOfferDto>>();

        foreach (var offer in offers)
        {
            if (!shipments.TryGetValue(offer.ShipmentId, out var shipment) || shipment.Status != ShipmentStatus.Pending)
            {
                continue;
            }

            var accepted = offer.Status == ShipmentOfferStatus.Accepted && shipment.TruckingCompanyId == companyId;
            var waiting = offer.IsWaiting(now, shipment.OfferDeadline);
            if (!accepted && !waiting)
            {
                continue;
            }

            DateTime? waitingUntil = waiting
                ? offer.LimitAt is { } limit && limit < shipment.OfferDeadline ? limit : shipment.OfferDeadline
                : null;

            var dto = new TrackingOfferDto(
                offer.Id,
                shipment.Id,
                offer.Status,
                shipment.PickupPlaceName,
                shipment.DeliveryPlaceName,
                shipperNames.GetValueOrDefault(shipment.ShipperId) ?? string.Empty,
                shipment.Load.WeightKg,
                shipment.Load.VolumeCubicMeters,
                shipment.PickupWindow.Earliest,
                shipment.PickupWindow.Latest,
                shipment.DeliveryWindow.Earliest,
                shipment.DeliveryWindow.Latest,
                offer.PriceEur,
                waitingUntil);

            if (!byTruck.TryGetValue(offer.TruckId, out var list))
            {
                byTruck[offer.TruckId] = list = [];
            }

            list.Add(dto);
        }

        // Accepted first, then by pickup time.
        foreach (var list in byTruck.Values)
        {
            list.Sort((a, b) => a.Status != b.Status
                ? (a.Status == ShipmentOfferStatus.Accepted ? -1 : 1)
                : a.PickupWindowEarliest.CompareTo(b.PickupWindowEarliest));
        }

        return byTruck;
    }

    private static string PlaceName(Stop stop, IReadOnlyDictionary<Guid, Shipment> shipments, TruckingCompany company) =>
        stop.Kind switch
        {
            StopKind.Pickup when stop.ShipmentId is { } id && shipments.TryGetValue(id, out var shipment) => shipment.PickupPlaceName,
            StopKind.Delivery when stop.ShipmentId is { } id && shipments.TryGetValue(id, out var shipment) => shipment.DeliveryPlaceName,
            StopKind.Office => company.OfficePlaceName,
            _ => string.Empty,
        };
}
