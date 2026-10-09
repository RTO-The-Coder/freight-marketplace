using Freight.Application.Client;
using Freight.Application.Offers;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("shipments")]
public sealed class ShipmentsController(
    BookShipmentHandler bookShipmentHandler,
    UpdateShipmentWindowsHandler updateShipmentWindowsHandler,
    GetPendingShipmentsHandler getPendingShipmentsHandler,
    GetShipmentOffersHandler getShipmentOffersHandler) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<GetPendingShipmentsResponse>> GetPendingShipments(CancellationToken cancellationToken)
    {
        var response = await getPendingShipmentsHandler.GetPendingShipmentsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<BookShipmentResponse>> BookShipment(
        BookShipmentBody body,
        CancellationToken cancellationToken)
    {
        var response = await bookShipmentHandler.BookShipmentAsync(
            new BookShipmentRequest(
                body.ShipperId,
                GeoLocation.Create(body.PickupLatitude, body.PickupLongitude),
                GeoLocation.Create(body.DeliveryLatitude, body.DeliveryLongitude),
                Capacity.Create(body.LoadWeightKg, body.LoadVolumeCubicMeters),
                body.RequiredTruckType,
                TimeWindow.Create(body.PickupWindowEarliest, body.PickupWindowLatest),
                TimeWindow.Create(body.DeliveryWindowEarliest, body.DeliveryWindowLatest),
                body.TruckingCompanyId),
            cancellationToken);
        return Ok(response);
    }

    [HttpPatch("{shipmentId:guid}/windows")]
    public async Task<IActionResult> UpdateWindows(
        Guid shipmentId,
        UpdateShipmentWindowsBody body,
        CancellationToken cancellationToken)
    {
        await updateShipmentWindowsHandler.HandleAsync(
            new UpdateShipmentWindowsRequest(
                shipmentId,
                TimeWindow.Create(body.PickupWindowEarliest, body.PickupWindowLatest),
                TimeWindow.Create(body.DeliveryWindowEarliest, body.DeliveryWindowLatest)),
            cancellationToken);
        return NoContent();
    }

    [HttpGet("{shipmentId:guid}/offers")]
    public async Task<ActionResult<GetShipmentOffersResponse>> GetOffers(Guid shipmentId, CancellationToken cancellationToken)
    {
        var response = await getShipmentOffersHandler.GetOffersAsync(new GetShipmentOffersRequest(shipmentId), cancellationToken);
        return Ok(response);
    }
}

/// <param name="TruckingCompanyId">Optional: book the shipment straight to this company (no offers).</param>
public sealed record BookShipmentBody(
    Guid ShipperId,
    double PickupLatitude,
    double PickupLongitude,
    double DeliveryLatitude,
    double DeliveryLongitude,
    double LoadWeightKg,
    double LoadVolumeCubicMeters,
    TruckType RequiredTruckType,
    DateTime PickupWindowEarliest,
    DateTime PickupWindowLatest,
    DateTime DeliveryWindowEarliest,
    DateTime DeliveryWindowLatest,
    Guid? TruckingCompanyId = null);

public sealed record UpdateShipmentWindowsBody(
    DateTime PickupWindowEarliest,
    DateTime PickupWindowLatest,
    DateTime DeliveryWindowEarliest,
    DateTime DeliveryWindowLatest);
