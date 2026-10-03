using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Notifications;
using Freight.Domain.Notifications.Abstractions;
using Freight.Domain.ValueObjects;
using ShipmentAggregate = Freight.Domain.Client.Shipment;

namespace Freight.Application.Client;

public sealed record BookShipmentRequest(
    Guid ShipperId,
    GeoLocation PickupLocation,
    GeoLocation DeliveryLocation,
    Capacity Load,
    TruckType RequiredTruckType,
    TimeWindow PickupWindow,
    TimeWindow DeliveryWindow);

public sealed record BookShipmentResponse(Guid ShipmentId);

public sealed class BookShipmentHandler(
    IUnitOfWork unitOfWork, TimeProvider timeProvider, INotificationSender notificationSender)
{
    public async Task<BookShipmentResponse> BookShipmentAsync(BookShipmentRequest request, CancellationToken cancellationToken = default)
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

        // Unconditional - every TruckingCompany, no eligibility filtering (ADR 0007).
        // Runs synchronously since there is no search here, just a notification send.
        var summary = new ShipmentNotificationSummary(
            shipment.Id, shipment.PickupLocation, shipment.RequiredTruckType, shipment.PickupWindow);
        await notificationSender.NotifyAllCompaniesAsync(summary, cancellationToken);

        return new BookShipmentResponse(shipment.Id);
    }
}
