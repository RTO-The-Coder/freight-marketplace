using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using ShipmentAggregate = Freight.Domain.Client.Shipment;

namespace Freight.Application.Shipments;

public sealed record BookShipmentRequest(
    Guid ShipperId,
    GeoLocation PickupLocation,
    GeoLocation DeliveryLocation,
    Capacity Load,
    TruckType RequiredTruckType,
    TimeWindow PickupWindow,
    TimeWindow DeliveryWindow);

public sealed record BookShipmentResponse(Guid ShipmentId);

public sealed class BookShipmentHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<BookShipmentResponse> HandleAsync(BookShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        var shipment = ShipmentAggregate.Book(
            request.ShipperId,
            request.PickupLocation,
            request.DeliveryLocation,
            request.Load,
            request.RequiredTruckType,
            request.PickupWindow,
            request.DeliveryWindow,
            clock.CurrentTime);

        unitOfWork.Shipments.Add(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BookShipmentResponse(shipment.Id);
    }
}
