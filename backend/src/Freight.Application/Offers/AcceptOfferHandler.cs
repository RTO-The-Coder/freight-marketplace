using Freight.Domain.Client.Enums;
using Freight.Domain.Common;

namespace Freight.Application.Offers;

public sealed record AcceptOfferRequest(Guid OfferId);

public sealed record AcceptOfferResponse(Guid ShipmentId, Guid TruckingCompanyId);

/// <summary>
/// The shipper accepts one waiting offer: it wins, every other Pending offer on the shipment is
/// rejected, and the shipment now belongs to the winning company - still Pending. No stops are
/// created here; the company does that with "Add to trip" (<see cref="AddOfferToTripHandler"/>).
/// Not guarded against two simultaneous accepts (known limitation).
/// </summary>
public sealed class AcceptOfferHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<AcceptOfferResponse> AcceptOfferAsync(AcceptOfferRequest request, CancellationToken cancellationToken = default)
    {
        var offer = await unitOfWork.ShipmentOffers.GetByIdAsync(request.OfferId, cancellationToken)
            ?? throw new InvalidOperationException($"Offer '{request.OfferId}' was not found.");

        var shipment = await unitOfWork.Shipments.GetByIdAsync(offer.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{offer.ShipmentId}' was not found.");

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var now = clock.CurrentTime;

        offer.Accept(now, shipment.OfferDeadline);
        shipment.AcceptOffer(offer.TruckingCompanyId, now);

        var otherOffers = await unitOfWork.ShipmentOffers.GetByShipmentIdAsync(shipment.Id, cancellationToken);
        foreach (var other in otherOffers.Where(other => other.Id != offer.Id && other.Status == ShipmentOfferStatus.Pending))
        {
            other.Reject();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcceptOfferResponse(shipment.Id, offer.TruckingCompanyId);
    }
}
