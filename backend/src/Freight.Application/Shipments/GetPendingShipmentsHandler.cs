using Freight.Domain.Common;
using Freight.Domain.Shipment;

namespace Freight.Application.Shipments;

public sealed record GetPendingShipmentsResponse(IReadOnlyList<ShipmentSummaryDto> Shipments);

public sealed class GetPendingShipmentsHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetPendingShipmentsResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var shipments = await unitOfWork.Shipments.GetByStatusAsync(ShipmentStatus.Pending, cancellationToken);

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

        return new GetPendingShipmentsResponse(dtos);
    }
}
