using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Fleet.Enums;

namespace Freight.Application.Client;

/// <summary>
/// Flat snapshot of a <see cref="Shipment"/> for shipper- and marketplace-facing lists.
/// Shared by <see cref="GetShipmentsByShipperHandler"/>, <see cref="GetPendingShipmentsHandler"/>
/// and the company shipment board - build it with <see cref="From"/> so the projection lives in
/// one place. <see cref="OffersOpen"/> is as of the simulation clock's "now" passed to From;
/// <see cref="WaitingOfferCount"/> is only filled for the shipper's own list.
/// </summary>
public sealed record ShipmentSummaryDto(
    Guid ShipmentId,
    Guid? TruckingCompanyId,
    double PickupLatitude,
    double PickupLongitude,
    double DeliveryLatitude,
    double DeliveryLongitude,
    double LoadWeightKg,
    double LoadVolumeCubicMeters,
    TruckType RequiredTruckType,
    DateTime PickupWindowEarliest,
    DateTime PickupWindowLatest,
    DateTime DeliveryWindowEarliest,
    DateTime DeliveryWindowLatest,
    DateTime OfferDeadline,
    ShipmentStatus Status,
    bool IsDirect,
    bool OffersOpen,
    int WaitingOfferCount)
{
    public static ShipmentSummaryDto From(Shipment shipment, DateTime now, int waitingOfferCount = 0) => new(
        shipment.Id,
        shipment.TruckingCompanyId,
        shipment.PickupLocation.Latitude,
        shipment.PickupLocation.Longitude,
        shipment.DeliveryLocation.Latitude,
        shipment.DeliveryLocation.Longitude,
        shipment.Load.WeightKg,
        shipment.Load.VolumeCubicMeters,
        shipment.RequiredTruckType,
        shipment.PickupWindow.Earliest,
        shipment.PickupWindow.Latest,
        shipment.DeliveryWindow.Earliest,
        shipment.DeliveryWindow.Latest,
        shipment.OfferDeadline,
        shipment.Status,
        shipment.IsDirect,
        shipment.IsOpenForOffers(now),
        waitingOfferCount);
}
