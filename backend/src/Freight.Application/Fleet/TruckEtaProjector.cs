using Freight.Domain.Client;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Fleet;

/// <summary>
/// A truck's open trip projected forward: where the projection starts, the projected
/// arrival at each Pending stop, the truck's status and - when parked - the window it waits for.
/// </summary>
public sealed record TruckTripProjection(
    DateTime ProjectionStart,
    IReadOnlyDictionary<Guid, DateTime> Etas,
    TruckStatus Status,
    WaitingInfo? Waiting);

/// <summary>
/// Projects a truck's open trip with <see cref="RouteEtaCalculator"/> - the same forward
/// route walk the shipment-insertion feasibility check uses, including its wait-for-window
/// modelling. Shared by the truck's own ETAs (<see cref="GetTruckEtasHandler"/>) and the
/// company's Tracking page, which projects every running trip at once.
///
/// The projection runs from where the truck's primary-driver ledger currently stands (its
/// <c>LastEvaluatedSimulatedTime</c>), carrying the truck's live leg progress into the
/// first Pending leg.
/// </summary>
public sealed class TruckEtaProjector(RouteEtaCalculator routeEtaCalculator)
{
    public TruckTripProjection Project(Truck truck, Trip trip, IReadOnlyDictionary<Guid, TimeWindow> stopWindows)
    {
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

        var nextStop = trip.NextStop;
        var nextStopWindow = nextStop is not null && stopWindows.TryGetValue(nextStop.Id, out var w) ? w : null;
        var status = truck.DetermineStatus(trip, projectionStart, nextStopWindow);

        WaitingInfo? waiting = null;
        if (status == TruckStatus.Parked && nextStop is not null && nextStopWindow is not null)
        {
            waiting = new WaitingInfo(nextStop.Id, nextStop.Kind, nextStopWindow.Earliest);
        }

        return new TruckTripProjection(projectionStart, projection.Etas, status, waiting);
    }

    /// <summary>
    /// Each Pending shipment stop's own requested window, keyed by stop id - a Pickup
    /// stop's pickup window, a Delivery stop's delivery window. Office stops have none.
    /// <paramref name="shipmentsById"/> must hold every shipment the trip's stops reference.
    /// </summary>
    public static IReadOnlyDictionary<Guid, TimeWindow> StopWindows(Trip trip, IReadOnlyDictionary<Guid, Shipment> shipmentsById)
    {
        var windows = new Dictionary<Guid, TimeWindow>();

        foreach (var stop in trip.Stops)
        {
            if (stop.Status != StopStatus.Pending || stop.ShipmentId is not { } shipmentId
                || stop.Kind is not (StopKind.Pickup or StopKind.Delivery))
            {
                continue;
            }

            var shipment = shipmentsById.GetValueOrDefault(shipmentId)
                ?? throw new InvalidOperationException($"Shipment '{shipmentId}' referenced by stop '{stop.Id}' was not found.");

            windows[stop.Id] = stop.Kind == StopKind.Pickup ? shipment.PickupWindow : shipment.DeliveryWindow;
        }

        return windows;
    }
}
