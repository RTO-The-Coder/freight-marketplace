using Freight.Application.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Client;

public sealed record GetShipmentsByShipperRequest(Guid ShipperId);

public sealed record GetShipmentsByShipperResponse(IReadOnlyList<ShipmentSummaryDto> Shipments);

/// <summary>
/// The shipper's own shipments, each with its waiting-offer count and where it is now: at its
/// pickup until picked up, on the carrying truck while in transit (that truck's live position,
/// <see cref="TruckPositionLocator"/>), at its delivery once delivered.
/// </summary>
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

        var positions = await CurrentPositionsAsync(shipments, cancellationToken);

        var dtos = shipments
            .Select(shipment => ShipmentSummaryDto.From(
                shipment,
                now,
                offersByShipment[shipment.Id].Count(offer => offer.IsWaiting(now, shipment.OfferDeadline)),
                positions[shipment.Id]))
            .ToList();

        return new GetShipmentsByShipperResponse(dtos);
    }

    /// <summary>Where each shipment is now, by shipment id. Open trips are read only when one is in transit.</summary>
    private async Task<Dictionary<Guid, GeoLocation>> CurrentPositionsAsync(
        IReadOnlyList<Shipment> shipments, CancellationToken cancellationToken)
    {
        var positions = shipments.ToDictionary(
            shipment => shipment.Id,
            shipment => shipment.Status == ShipmentStatus.Delivered ? shipment.DeliveryLocation : shipment.PickupLocation);

        var inTransit = shipments.Where(shipment => shipment.Status == ShipmentStatus.InTransit).ToList();
        if (inTransit.Count == 0)
        {
            return positions;
        }

        var openTrips = await unitOfWork.Trips.GetOpenTripsAsync(cancellationToken);
        foreach (var shipment in inTransit)
        {
            // An in-transit shipment is on board the truck whose open trip still has its delivery stop.
            var trip = openTrips.FirstOrDefault(t => t.Stops.Any(stop => stop.ShipmentId == shipment.Id));
            if (trip is null)
            {
                continue;
            }

            var truck = await unitOfWork.Trucks.GetByIdAsync(trip.TruckId, cancellationToken);
            var company = await unitOfWork.TruckingCompanies.GetByIdAsync(trip.TruckingCompanyId, cancellationToken);
            if (truck is not null && company is not null)
            {
                positions[shipment.Id] = TruckPositionLocator.Locate(truck, trip, company.OfficeLocation).Position;
            }
        }

        return positions;
    }
}
