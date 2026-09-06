using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Fleet;

public sealed record GetTruckEtasRequest(Guid TruckId);

public sealed record TruckEtaStopDto(
    Guid StopId,
    Guid? ShipmentId,
    StopKind Kind,
    StopStatus Status,
    int Sequence,
    DateTime? ReachedAt,
    DateTime? ProjectedArrival,
    int WaitTimeTick,
    int WaitTimeTickElapsed);

public sealed record TruckEtasDto(
    Guid TruckId,
    Guid? TripId,
    DateTime? ProjectionStart,
    TruckStatus Status,
    WaitingInfo? Waiting,
    IReadOnlyList<TruckEtaStopDto> Stops);

/// <summary>
/// Projected arrival time at every stop of a truck's current open trip, computed by
/// <see cref="RouteEtaCalculator"/> - the same forward route walk the shipment-insertion
/// feasibility check uses, including its wait-for-window modelling. Reached stops report
/// their actual <c>ReachedAt</c> and no projection; still-Pending stops report the
/// projected physical arrival and any planned/served wait. A truck with no open trip
/// returns an empty stop list.
///
/// The projection runs from where the truck's primary-driver ledger currently stands
/// (its <c>LastEvaluatedSimulatedTime</c>), carrying the truck's live leg progress into
/// the first Pending leg.
/// </summary>
public sealed class GetTruckEtasHandler(IUnitOfWork unitOfWork, RouteEtaCalculator routeEtaCalculator)
{
    public async Task<TruckEtasDto> GetTruckEtasAsync(GetTruckEtasRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
        if (trip is null)
        {
            return new TruckEtasDto(truck.Id, null, null, truck.DetermineStatus(null), null, []);
        }

        if (truck.DriverAssignment is null)
        {
            throw new InvalidOperationException($"Truck '{truck.Id}' has an open trip but no driver assignment.");
        }

        var assignment = truck.DriverAssignment;
        var primary = assignment.PrimaryDriver;
        var primaryLedger = primary.ComplianceState
            ?? throw new InvalidOperationException($"Open trip '{trip.Id}' has no compliance ledger for its primary driver.");

        // Both ledgers share LastEvaluatedSimulatedTime (EvaluateTeam sets both), so the
        // primary's is the projection start for a team truck too.
        var projectionStart = primaryLedger.LastEvaluatedSimulatedTime;

        var stopWindows = await BuildStopWindowsAsync(trip, cancellationToken);

        RouteProjection projection;
        if (assignment.ConfigurationType == DriverConfigurationType.Team)
        {
            var secondary = assignment.SecondaryDriver
                ?? throw new InvalidOperationException($"Team truck '{truck.Id}' has no secondary driver.");
            var secondaryLedger = secondary.ComplianceState
                ?? throw new InvalidOperationException($"Open trip '{trip.Id}' has no compliance ledger for its secondary driver.");

            projection = routeEtaCalculator.CalculateEtasForTeam(
                trip, truck.CurrentProgress,
                primaryLedger, primary.Rules,
                secondaryLedger, secondary.Rules,
                assignment.ActiveDriverId ?? primary.Id,
                projectionStart,
                stopWindows);
        }
        else
        {
            projection = routeEtaCalculator.CalculateEtas(
                trip, truck.CurrentProgress, primaryLedger, primary.Rules, projectionStart, stopWindows);
        }

        var stops = trip.Stops
            .Select(stop => new TruckEtaStopDto(
                stop.Id,
                stop.ShipmentId,
                stop.Kind,
                stop.Status,
                stop.Sequence,
                stop.ReachedAt,
                projection.Etas.TryGetValue(stop.Id, out var eta) ? eta : null,
                stop.WaitTimeTick,
                stop.WaitTimeTickElapsed))
            .ToList();

        var nextStop = trip.NextStop;
        var nextStopWindow = nextStop is not null && stopWindows.TryGetValue(nextStop.Id, out var w) ? w : null;
        var status = truck.DetermineStatus(trip, projectionStart, nextStopWindow);

        WaitingInfo? waiting = null;
        if (status == TruckStatus.Parked && nextStop is not null && nextStopWindow is not null)
        {
            waiting = new WaitingInfo(nextStop.Id, nextStop.Kind, nextStopWindow.Earliest);
        }

        return new TruckEtasDto(truck.Id, trip.Id, projectionStart, status, waiting, stops);
    }

    /// <summary>
    /// Each Pending shipment stop's own requested window, keyed by stop id - a Pickup
    /// stop's pickup window, a Delivery stop's delivery window. Office stops have none.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, TimeWindow>> BuildStopWindowsAsync(
        Trip trip, CancellationToken cancellationToken)
    {
        var windows = new Dictionary<Guid, TimeWindow>();

        foreach (var stop in trip.Stops)
        {
            if (stop.Status != StopStatus.Pending || stop.ShipmentId is not { } shipmentId
                || stop.Kind is not (StopKind.Pickup or StopKind.Delivery))
            {
                continue;
            }

            var shipment = await unitOfWork.Shipments.GetByIdAsync(shipmentId, cancellationToken)
                ?? throw new InvalidOperationException($"Shipment '{shipmentId}' referenced by stop '{stop.Id}' was not found.");

            windows[stop.Id] = stop.Kind == StopKind.Pickup ? shipment.PickupWindow : shipment.DeliveryWindow;
        }

        return windows;
    }
}
