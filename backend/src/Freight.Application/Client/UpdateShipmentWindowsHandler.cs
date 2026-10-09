using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.Notifications;
using Freight.Domain.Notifications.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Client;

public sealed record UpdateShipmentWindowsRequest(Guid ShipmentId, TimeWindow PickupWindow, TimeWindow DeliveryWindow);

/// <summary>
/// The shipper changes a Pending shipment's times - the way a "dead" open shipment (offer window
/// passed with nothing accepted) is put back on the market. Restarts the two-hour offer window,
/// rejects every Pending offer (they were made for the old times), and pushes the shipment again:
/// to every company for an open shipment, only to its company for a direct one.
/// </summary>
public sealed class UpdateShipmentWindowsHandler(
    IUnitOfWork unitOfWork, TimeProvider timeProvider, INotificationSender notificationSender)
{
    public async Task HandleAsync(UpdateShipmentWindowsRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await unitOfWork.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{request.ShipmentId}' was not found.");

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        shipment.UpdateWindows(request.PickupWindow, request.DeliveryWindow, clock.CurrentTime);

        var offers = await unitOfWork.ShipmentOffers.GetByShipmentIdAsync(shipment.Id, cancellationToken);
        foreach (var offer in offers.Where(offer => offer.Status == ShipmentOfferStatus.Pending))
        {
            offer.Reject();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var summary = new ShipmentNotificationSummary(
            shipment.Id, shipment.PickupLocation, shipment.RequiredTruckType, shipment.PickupWindow, shipment.IsDirect);
        if (shipment.IsDirect)
        {
            await notificationSender.NotifyCompanyAsync(shipment.TruckingCompanyId!.Value, summary, cancellationToken);
        }
        else
        {
            await notificationSender.NotifyAllCompaniesAsync(summary, cancellationToken);
        }
    }
}
