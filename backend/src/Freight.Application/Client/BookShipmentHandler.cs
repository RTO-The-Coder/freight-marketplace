using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Notifications;
using Freight.Domain.Notifications.Abstractions;
using Freight.Domain.ValueObjects;
using ShipmentAggregate = Freight.Domain.Client.Shipment;

namespace Freight.Application.Client;

/// <param name="TruckingCompanyId">Set to book the shipment straight to that company (no offers); null for an open shipment.</param>
public sealed record BookShipmentRequest(
    Guid ShipperId,
    GeoLocation PickupLocation,
    GeoLocation DeliveryLocation,
    Capacity Load,
    TruckType RequiredTruckType,
    TimeWindow PickupWindow,
    TimeWindow DeliveryWindow,
    Guid? TruckingCompanyId = null);

public sealed record BookShipmentResponse(Guid ShipmentId);

public sealed class BookShipmentHandler(
    IUnitOfWork unitOfWork, TimeProvider timeProvider, INotificationSender notificationSender)
{
    public async Task<BookShipmentResponse> BookShipmentAsync(BookShipmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TruckingCompanyId is { } companyId
            && await unitOfWork.TruckingCompanies.GetByIdAsync(companyId, cancellationToken) is null)
        {
            throw new InvalidOperationException($"Trucking company '{companyId}' was not found.");
        }

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
            clock.CurrentTime,
            request.TruckingCompanyId);

        unitOfWork.Shipments.Add(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // No search here, just a notification send, so it runs synchronously (ADR 0007):
        // an open shipment goes to every TruckingCompany, a direct one only to its company.
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

        return new BookShipmentResponse(shipment.Id);
    }
}
