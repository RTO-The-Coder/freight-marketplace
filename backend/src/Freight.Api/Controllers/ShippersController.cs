using Freight.Application.Client;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("shippers")]
public sealed class ShippersController(
    GetShippersHandler getShippersHandler,
    GetShipmentsByShipperHandler getShipmentsByShipperHandler) : ControllerBase
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
}
