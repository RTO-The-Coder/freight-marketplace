using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;

namespace Freight.Application.Tracking;

/// <summary>Where a shipment on a trip stands: picked up and delivered, on board, or still to pick up.</summary>
public enum TrackedShipmentState
{
    Delivered,
    OnBoard,
    ToPick,
}

public sealed record TrackingDriverDto(Guid DriverId, string Name);

/// <summary>
/// A shipment added to one of the company's trips, as the Tracking page shows it.
/// <see cref="Eta"/> is the projected arrival at its next stop (the pickup while
/// <see cref="TrackedShipmentState.ToPick"/>, the delivery while
/// <see cref="TrackedShipmentState.OnBoard"/>); null once delivered or when no projection
/// is available. <see cref="LateByMinutes"/> is how far the later of its pickup/delivery
/// (reached, or projected) falls after that stop's window end - 0 when on time, null when
/// not known yet. <see cref="PriceEur"/> is the accepted offer's price - null for a direct
/// booking.
/// </summary>
public sealed record TrackedShipmentDto(
    Guid ShipmentId,
    TrackedShipmentState State,
    string FromPlace,
    string ToPlace,
    string ShipperName,
    double LoadWeightKg,
    double LoadVolumeCubicMeters,
    DateTime PickupWindowEarliest,
    DateTime PickupWindowLatest,
    DateTime DeliveryWindowEarliest,
    DateTime DeliveryWindowLatest,
    DateTime? PickedUpAt,
    DateTime? DeliveredAt,
    DateTime? Eta,
    int? LateByMinutes,
    decimal? PriceEur);

/// <summary>
/// Builds the Tracking page's shipment rows and driver names from a trip - shared by the
/// running-trips view (<see cref="GetCompanyTrackingHandler"/>) and the history
/// (<see cref="GetTrackingHistoryHandler"/>).
/// </summary>
public static class TrackedShipments
{
    /// <summary>
    /// One row per shipment on <paramref name="trip"/>, in route order (by the position of its
    /// pickup stop). <paramref name="shipments"/> must hold every shipment the trip references;
    /// <paramref name="etas"/> is the projection's arrival per Pending stop id (empty for a
    /// completed trip).
    /// </summary>
    public static IReadOnlyList<TrackedShipmentDto> Build(
        Trip trip,
        IReadOnlyDictionary<Guid, Shipment> shipments,
        IReadOnlyDictionary<Guid, string> shipperNames,
        IReadOnlyDictionary<Guid, decimal> pricesByShipment,
        IReadOnlyDictionary<Guid, DateTime> etas)
    {
        var rows = new List<TrackedShipmentDto>();

        foreach (var pickup in trip.Stops.Where(stop => stop.Kind == StopKind.Pickup && stop.ShipmentId is not null))
        {
            var shipmentId = pickup.ShipmentId!.Value;
            var delivery = trip.Stops.FirstOrDefault(stop => stop.Kind == StopKind.Delivery && stop.ShipmentId == shipmentId);
            var shipment = shipments.GetValueOrDefault(shipmentId)
                ?? throw new InvalidOperationException($"Shipment '{shipmentId}' referenced by trip '{trip.Id}' was not found.");

            var pickedUp = pickup.Status == StopStatus.Reached;
            var delivered = delivery?.Status == StopStatus.Reached;
            var state = delivered ? TrackedShipmentState.Delivered
                : pickedUp ? TrackedShipmentState.OnBoard
                : TrackedShipmentState.ToPick;

            var pickupEta = etas.TryGetValue(pickup.Id, out var pe) ? pe : (DateTime?)null;
            var deliveryEta = delivery is not null && etas.TryGetValue(delivery.Id, out var de) ? de : (DateTime?)null;

            var pickupAt = pickup.ReachedAt ?? pickupEta;
            var deliveryAt = delivery?.ReachedAt ?? deliveryEta;

            rows.Add(new TrackedShipmentDto(
                shipment.Id,
                state,
                shipment.PickupPlaceName,
                shipment.DeliveryPlaceName,
                shipperNames.GetValueOrDefault(shipment.ShipperId) ?? string.Empty,
                shipment.Load.WeightKg,
                shipment.Load.VolumeCubicMeters,
                shipment.PickupWindow.Earliest,
                shipment.PickupWindow.Latest,
                shipment.DeliveryWindow.Earliest,
                shipment.DeliveryWindow.Latest,
                pickup.ReachedAt,
                delivery?.ReachedAt,
                state switch
                {
                    TrackedShipmentState.ToPick => pickupEta,
                    TrackedShipmentState.OnBoard => deliveryEta,
                    _ => null,
                },
                LateBy(pickupAt, shipment.PickupWindow.Latest, deliveryAt, shipment.DeliveryWindow.Latest),
                pricesByShipment.TryGetValue(shipment.Id, out var price) ? price : null));
        }

        return rows;
    }

    /// <summary>The trip's recorded drivers as "First Last".</summary>
    public static IReadOnlyList<TrackingDriverDto> Drivers(Trip trip) =>
        [.. trip.Drivers.Select(driver => new TrackingDriverDto(driver.DriverId, $"{driver.FirstName} {driver.LastName}"))];

    /// <summary>The truck's currently assigned drivers as "First Last" - for a trip opened before drivers were recorded.</summary>
    public static IReadOnlyList<TrackingDriverDto> Drivers(Truck truck)
    {
        if (truck.DriverAssignment is not { } assignment)
        {
            return [];
        }

        var drivers = new List<Driver> { assignment.PrimaryDriver };
        if (assignment.SecondaryDriver is { } secondary)
        {
            drivers.Add(secondary);
        }

        return [.. drivers.Select(driver => new TrackingDriverDto(driver.Id, $"{driver.FirstName} {driver.LastName}"))];
    }

    /// <summary>
    /// The company's price for each of these shipments - the accepted offer's price. A
    /// shipment with no accepted offer (a direct booking) has none.
    /// </summary>
    public static IReadOnlyDictionary<Guid, decimal> Prices(IEnumerable<ShipmentOffer> companyOffers) =>
        companyOffers
            .Where(offer => offer.Status == ShipmentOfferStatus.Accepted)
            .GroupBy(offer => offer.ShipmentId)
            .ToDictionary(group => group.Key, group => group.First().PriceEur);

    /// <summary>Each shipment's shipper name, keyed by shipper id - one lookup per distinct shipper.</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> ShipperNamesAsync(
        IUnitOfWork unitOfWork, IEnumerable<Shipment> shipments, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var shipperId in shipments.Select(shipment => shipment.ShipperId).Distinct())
        {
            if (await unitOfWork.Shippers.GetByIdAsync(shipperId, cancellationToken) is { } shipper)
            {
                names[shipperId] = shipper.Name;
            }
        }

        return names;
    }

    private static int? LateBy(DateTime? pickupAt, DateTime pickupLatest, DateTime? deliveryAt, DateTime deliveryLatest)
    {
        if (pickupAt is null || deliveryAt is null)
        {
            return null;
        }

        var late = Math.Max((pickupAt.Value - pickupLatest).TotalMinutes, (deliveryAt.Value - deliveryLatest).TotalMinutes);
        return late > 0 ? (int)Math.Ceiling(late) : 0;
    }
}
