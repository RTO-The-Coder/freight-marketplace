using Freight.Domain.Common;

namespace Freight.Application.Offers;

public sealed record GetShipmentOffersRequest(Guid ShipmentId);

/// <summary>An offer as the shipper sees it. <see cref="LimitAt"/> null = valid until offers close.</summary>
public sealed record ShipperOfferDto(
    Guid OfferId,
    Guid TruckingCompanyId,
    string CompanyName,
    decimal PriceEur,
    DateTime? LimitAt,
    DateTime CreatedAt);

public sealed record GetShipmentOffersResponse(Guid ShipmentId, DateTime OfferDeadline, IReadOnlyList<ShipperOfferDto> Offers);

/// <summary>
/// The shipper's "See offers": the shipment's waiting offers only - rejected offers, and offers
/// past their own limit or the shipment's offer deadline, are left out (kept in storage).
/// </summary>
public sealed class GetShipmentOffersHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<GetShipmentOffersResponse> GetOffersAsync(GetShipmentOffersRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await unitOfWork.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{request.ShipmentId}' was not found.");

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        var offers = await unitOfWork.ShipmentOffers.GetByShipmentIdAsync(shipment.Id, cancellationToken);
        var waiting = offers.Where(offer => offer.IsWaiting(clock.CurrentTime, shipment.OfferDeadline)).ToList();

        var companyNames = new Dictionary<Guid, string>();
        foreach (var companyId in waiting.Select(offer => offer.TruckingCompanyId).Distinct())
        {
            var company = await unitOfWork.TruckingCompanies.GetByIdAsync(companyId, cancellationToken);
            companyNames[companyId] = company?.Name ?? "Unknown company";
        }

        var dtos = waiting
            .OrderBy(offer => offer.PriceEur)
            .Select(offer => new ShipperOfferDto(
                offer.Id, offer.TruckingCompanyId, companyNames[offer.TruckingCompanyId], offer.PriceEur, offer.LimitAt, offer.CreatedAt))
            .ToList();

        return new GetShipmentOffersResponse(shipment.Id, shipment.OfferDeadline, dtos);
    }
}
