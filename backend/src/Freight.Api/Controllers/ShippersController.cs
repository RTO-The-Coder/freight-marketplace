using Freight.Application.Client;
using Freight.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("shippers")]
public sealed class ShippersController(
    GetShippersHandler getShippersHandler,
    GetShipmentsByShipperHandler getShipmentsByShipperHandler,
    GetShipperAddressesHandler getShipperAddressesHandler,
    AddShipperAddressHandler addShipperAddressHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetShippersResponse>> GetShippers(CancellationToken cancellationToken)
    {
        var response = await getShippersHandler.GetShippersAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{shipperId:guid}/shipments")]
    public async Task<ActionResult<GetShipmentsByShipperResponse>> GetShipmentsByShipper(
        Guid shipperId,
        CancellationToken cancellationToken)
    {
        var response = await getShipmentsByShipperHandler.GetShipmentsByShipperAsync(new GetShipmentsByShipperRequest(shipperId), cancellationToken);
        return Ok(response);
    }

    /// <summary>The shipper's saved addresses (address book), newest first.</summary>
    [HttpGet("{shipperId:guid}/addresses")]
    public async Task<ActionResult<GetShipperAddressesResponse>> GetAddresses(
        Guid shipperId,
        CancellationToken cancellationToken)
    {
        var response = await getShipperAddressesHandler.GetShipperAddressesAsync(new GetShipperAddressesRequest(shipperId), cancellationToken);
        return Ok(response);
    }

    /// <summary>Saves an address from a picked "Find area" match. Add-only: addresses can't be edited or deleted.</summary>
    [HttpPost("{shipperId:guid}/addresses")]
    public async Task<ActionResult<ShipperAddressDto>> AddAddress(
        Guid shipperId,
        AddShipperAddressBody body,
        CancellationToken cancellationToken)
    {
        var response = await addShipperAddressHandler.AddShipperAddressAsync(
            new AddShipperAddressRequest(shipperId, body.Label, body.AreaName, GeoLocation.Create(body.Latitude, body.Longitude)),
            cancellationToken);
        return Ok(response);
    }
}

/// <param name="Label">The shipper's own name for the address, e.g. "Main warehouse".</param>
/// <param name="AreaName">The picked match's area, e.g. "1100 Favoriten, Vienna".</param>
public sealed record AddShipperAddressBody(string Label, string AreaName, double Latitude, double Longitude);
