using Freight.Application.Shipments;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("shipments")]
public sealed class ShipmentsController(
    BookShipmentHandler bookShipmentHandler,
    UpdatePickupWindowHandler updatePickupWindowHandler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BookShipmentResponse>> BookShipment(
        BookShipmentBody body,
        CancellationToken cancellationToken)
    {
        var response = await bookShipmentHandler.HandleAsync(
            new BookShipmentRequest(
                body.ShipperId,
                GeoLocation.Create(body.PickupLatitude, body.PickupLongitude),
                GeoLocation.Create(body.DeliveryLatitude, body.DeliveryLongitude),
                Capacity.Create(body.LoadWeightKg, body.LoadVolumeCubicMeters),
                body.RequiredTruckType,
                TimeWindow.Create(body.PickupWindowEarliest, body.PickupWindowLatest),
                TimeWindow.Create(body.DeliveryWindowEarliest, body.DeliveryWindowLatest)),
            cancellationToken);
        return Ok(response);
    }

    [HttpPatch("{shipmentId:guid}/pickup-window")]
    public async Task<IActionResult> UpdatePickupWindow(
        Guid shipmentId,
        UpdatePickupWindowBody body,
        CancellationToken cancellationToken)
    {
        await updatePickupWindowHandler.HandleAsync(
            new UpdatePickupWindowRequest(
                shipmentId,
                TimeWindow.Create(body.PickupWindowEarliest, body.PickupWindowLatest)),
            cancellationToken);
        return NoContent();
    }
}

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
    DateTime DeliveryWindowLatest);

public sealed record UpdatePickupWindowBody(DateTime PickupWindowEarliest, DateTime PickupWindowLatest);
