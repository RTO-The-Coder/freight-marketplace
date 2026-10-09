using Freight.Application.Evaluation;
using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;

namespace Freight.Application.Offers;

/// <param name="LimitAt">The company's own offer limit; null means valid until the shipment's offer deadline.</param>
public sealed record OfferItem(Guid TruckId, decimal PriceEur, DateTime? LimitAt);

public sealed record SendOffersRequest(Guid TruckingCompanyId, Guid ShipmentId, IReadOnlyList<OfferItem> Offers);

public sealed record SendOffersResponse(IReadOnlyList<Guid> OfferIds);

/// <summary>
/// A company sends all its offers for one open shipment in one go - one per truck. The server
/// re-runs the company's eligibility once and stores each truck's evaluated pickup/delivery
/// positions and added distance on its offer; the client never supplies positions. After this,
/// the company can't add more offers for the shipment until the shipper changes its times.
/// </summary>
public sealed class SendOffersHandler(
    IUnitOfWork unitOfWork, ShipmentEvaluationEngine evaluationEngine, TimeProvider timeProvider)
{
    public async Task<SendOffersResponse> SendOffersAsync(SendOffersRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Offers.Count == 0)
        {
            throw new ArgumentException("Choose at least one truck to offer.", nameof(request));
        }

        if (request.Offers.Select(offer => offer.TruckId).Distinct().Count() != request.Offers.Count)
        {
            throw new ArgumentException("Each truck can only be offered once.", nameof(request));
        }

        var shipment = await unitOfWork.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{request.ShipmentId}' was not found.");

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var now = clock.CurrentTime;

        if (!shipment.IsOpenForOffers(now))
        {
            throw new InvalidOperationException("Offers on this shipment are closed.");
        }

        var existingOffers = await unitOfWork.ShipmentOffers.GetByShipmentIdAsync(shipment.Id, cancellationToken);
        if (existingOffers.Any(offer =>
                offer.TruckingCompanyId == request.TruckingCompanyId && offer.Status == ShipmentOfferStatus.Pending))
        {
            throw new InvalidOperationException("Your company has already sent its offers for this shipment.");
        }

        foreach (var item in request.Offers)
        {
            if (item.LimitAt is { } limitAt && (limitAt <= now || limitAt > shipment.OfferDeadline))
            {
                throw new ArgumentException(
                    $"An offer limit must be after now and no later than {shipment.OfferDeadline:g}, when offers close.",
                    nameof(request));
            }
        }

        var evaluation = await evaluationEngine.EvaluateForCompanyAsync(shipment.Id, request.TruckingCompanyId, cancellationToken);
        var resultsByTruck = evaluation.ToDictionary(result => result.TruckId);

        var offers = new List<ShipmentOffer>();
        foreach (var item in request.Offers)
        {
            if (!resultsByTruck.TryGetValue(item.TruckId, out var result))
            {
                throw new InvalidOperationException($"Truck '{item.TruckId}' does not belong to your company.");
            }

            if (!result.IsFeasible)
            {
                throw new InvalidOperationException($"Truck '{item.TruckId}' can't take this shipment.");
            }

            offers.Add(ShipmentOffer.Create(
                shipment.Id,
                request.TruckingCompanyId,
                item.TruckId,
                result.PickupInsertIndex!.Value,
                result.DeliveryInsertIndex!.Value,
                result.AddedDistanceKm ?? 0,
                item.PriceEur,
                item.LimitAt,
                now));
        }

        foreach (var offer in offers)
        {
            unitOfWork.ShipmentOffers.Add(offer);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SendOffersResponse(offers.Select(offer => offer.Id).ToList());
    }
}
