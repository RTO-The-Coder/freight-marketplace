using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;

namespace Freight.Application.Offers;

public sealed record GetCompanyShipmentBoardRequest(Guid TruckingCompanyId);

/// <summary>One of this company's offers. <see cref="LimitAt"/> null = valid until the shipment's offer deadline.</summary>
public sealed record CompanyOfferDto(
    Guid OfferId,
    Guid TruckId,
    string TruckName,
    int PickupInsertIndex,
    int DeliveryInsertIndex,
    double AddedDistanceKm,
    decimal PriceEur,
    DateTime? LimitAt,
    DateTime CreatedAt);

public sealed record OfferedShipmentDto(ShipmentSummaryDto Shipment, IReadOnlyList<CompanyOfferDto> Offers);

public sealed record ApprovedShipmentDto(ShipmentSummaryDto Shipment, CompanyOfferDto Offer);

public sealed record GetCompanyShipmentBoardResponse(
    IReadOnlyList<ShipmentSummaryDto> Open,
    IReadOnlyList<OfferedShipmentDto> Offered,
    IReadOnlyList<ApprovedShipmentDto> Approved,
    IReadOnlyList<ShipmentSummaryDto> Direct);

/// <summary>
/// A company's four shipment lists; every Pending shipment lands in at most one:
/// - Direct: booked straight to this company.
/// - Approved: the shipper accepted this company's offer - ready for "Add to trip".
/// - Offered: this company has offers still waiting (not past their limit or the offer deadline).
/// - Open: still open for offers and this company hasn't sent any yet.
/// Shipments whose offers or offer window have run out are in none of them.
/// </summary>
public sealed class GetCompanyShipmentBoardHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<GetCompanyShipmentBoardResponse> GetBoardAsync(
        GetCompanyShipmentBoardRequest request, CancellationToken cancellationToken = default)
    {
        var companyId = request.TruckingCompanyId;
        _ = await unitOfWork.TruckingCompanies.GetByIdAsync(companyId, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{companyId}' was not found.");

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var now = clock.CurrentTime;

        var pending = await unitOfWork.Shipments.GetByStatusAsync(ShipmentStatus.Pending, cancellationToken);
        var companyOffers = await unitOfWork.ShipmentOffers.GetByCompanyIdAsync(companyId, cancellationToken);
        var trucks = await unitOfWork.Trucks.GetByTruckingCompanyIdAsync(companyId, cancellationToken);
        var truckNames = trucks.ToDictionary(truck => truck.Id, truck => truck.TruckName);
        var offersByShipment = companyOffers.ToLookup(offer => offer.ShipmentId);

        var open = new List<ShipmentSummaryDto>();
        var offered = new List<OfferedShipmentDto>();
        var approved = new List<ApprovedShipmentDto>();
        var direct = new List<ShipmentSummaryDto>();

        foreach (var shipment in pending.OrderBy(shipment => shipment.PickupWindow.Earliest))
        {
            var ours = offersByShipment[shipment.Id].ToList();

            if (shipment.TruckingCompanyId == companyId)
            {
                if (shipment.IsDirect)
                {
                    direct.Add(ShipmentSummaryDto.From(shipment, now));
                }
                else if (ours.FirstOrDefault(offer => offer.Status == ShipmentOfferStatus.Accepted) is { } accepted)
                {
                    approved.Add(new ApprovedShipmentDto(ShipmentSummaryDto.From(shipment, now), ToDto(accepted, truckNames)));
                }

                continue;
            }

            var waiting = ours.Where(offer => offer.IsWaiting(now, shipment.OfferDeadline)).ToList();
            if (waiting.Count > 0)
            {
                offered.Add(new OfferedShipmentDto(
                    ShipmentSummaryDto.From(shipment, now),
                    waiting.OrderBy(offer => offer.PriceEur).Select(offer => ToDto(offer, truckNames)).ToList()));
            }
            else if (shipment.IsOpenForOffers(now) && ours.All(offer => offer.Status != ShipmentOfferStatus.Pending))
            {
                open.Add(ShipmentSummaryDto.From(shipment, now));
            }
        }

        return new GetCompanyShipmentBoardResponse(open, offered, approved, direct);
    }

    private static CompanyOfferDto ToDto(ShipmentOffer offer, IReadOnlyDictionary<Guid, string> truckNames) => new(
        offer.Id,
        offer.TruckId,
        truckNames.GetValueOrDefault(offer.TruckId) ?? $"Truck {offer.TruckId.ToString()[..8]}",
        offer.PickupInsertIndex,
        offer.DeliveryInsertIndex,
        offer.AddedDistanceKm,
        offer.PriceEur,
        offer.LimitAt,
        offer.CreatedAt);
}
