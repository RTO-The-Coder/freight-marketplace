using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Shipment;

namespace Freight.Application.Shipments;

public sealed record GetShipmentsByShipperRequest(Guid ShipperId);

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
    ShipmentStatus Status);

public sealed record GetShipmentsByShipperResponse(IReadOnlyList<ShipmentSummaryDto> Shipments);

public sealed class GetShipmentsByShipperHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetShipmentsByShipperResponse> HandleAsync(GetShipmentsByShipperRequest request, CancellationToken cancellationToken = default)
    {
        var shipments = await unitOfWork.Shipments.GetByShipperIdAsync(request.ShipperId, cancellationToken);

        var dtos = shipments
            .Select(shipment => new ShipmentSummaryDto(
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
                shipment.Status))
            .ToList();

        return new GetShipmentsByShipperResponse(dtos);
    }
}
