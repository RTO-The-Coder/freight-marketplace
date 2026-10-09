using Freight.Domain.Common;

namespace Freight.Application.Client;

public sealed record GetShipmentsByShipperRequest(Guid ShipperId);

public sealed record GetShipmentsByShipperResponse(IReadOnlyList<ShipmentSummaryDto> Shipments);

public sealed class GetShipmentsByShipperHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<GetShipmentsByShipperResponse> GetShipmentsByShipperAsync(GetShipmentsByShipperRequest request, CancellationToken cancellationToken = default)
    {
        var shipments = await unitOfWork.Shipments.GetByShipperIdAsync(request.ShipperId, cancellationToken);

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var now = clock.CurrentTime;

        // One query for every shipment's offers, for the "See offers (n)" counts.
        var offers = await unitOfWork.ShipmentOffers.GetByShipmentIdsAsync(
            shipments.Select(shipment => shipment.Id).ToList(), cancellationToken);
        var offersByShipment = offers.ToLookup(offer => offer.ShipmentId);

        var dtos = shipments
            .Select(shipment => ShipmentSummaryDto.From(
                shipment,
                now,
                offersByShipment[shipment.Id].Count(offer => offer.IsWaiting(now, shipment.OfferDeadline))))
            .ToList();

        return new GetShipmentsByShipperResponse(dtos);
    }
}
