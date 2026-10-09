using Freight.Application.Fleet;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;

namespace Freight.Application.Offers;

public sealed record AddOfferToTripRequest(Guid OfferId);

/// <summary>
/// The winning company's "Add to trip": inserts the shipment into the offered truck's trip at the
/// pickup/delivery positions stored on the accepted offer, via the existing, unchanged
/// <see cref="AssignShipmentToTruckHandler"/>. No feasibility re-check - if the truck's route has
/// changed so the positions no longer fit, that handler's own error is returned (known problem).
/// </summary>
public sealed class AddOfferToTripHandler(IUnitOfWork unitOfWork, AssignShipmentToTruckHandler assignShipmentToTruckHandler)
{
    public async Task<AssignShipmentToTruckResponse> AddToTripAsync(AddOfferToTripRequest request, CancellationToken cancellationToken = default)
    {
        var offer = await unitOfWork.ShipmentOffers.GetByIdAsync(request.OfferId, cancellationToken)
            ?? throw new InvalidOperationException($"Offer '{request.OfferId}' was not found.");

        if (offer.Status != ShipmentOfferStatus.Accepted)
        {
            throw new InvalidOperationException("Only an accepted offer can be added to a trip.");
        }

        var shipment = await unitOfWork.Shipments.GetByIdAsync(offer.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{offer.ShipmentId}' was not found.");

        if (shipment.Status != ShipmentStatus.Pending)
        {
            throw new InvalidOperationException("This shipment has already been added to a trip.");
        }

        return await assignShipmentToTruckHandler.AssignShipmentAsync(
            new AssignShipmentToTruckRequest(offer.TruckId, offer.ShipmentId, offer.PickupInsertIndex, offer.DeliveryInsertIndex),
            cancellationToken);
    }
}
