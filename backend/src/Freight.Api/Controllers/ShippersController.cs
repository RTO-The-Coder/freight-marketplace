using Freight.Application.Shipments;
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
        var response = await getShippersHandler.HandleAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{shipperId:guid}/shipments")]
    public async Task<ActionResult<GetShipmentsByShipperResponse>> GetShipmentsByShipper(
        Guid shipperId,
        CancellationToken cancellationToken)
    {
        var response = await getShipmentsByShipperHandler.HandleAsync(new GetShipmentsByShipperRequest(shipperId), cancellationToken);
        return Ok(response);
    }
}
