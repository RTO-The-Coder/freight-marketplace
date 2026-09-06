using Freight.Application.Routing;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
public sealed class RoutingController(
    GetRouteLegHandler getRouteLegHandler,
    GetRouteGeometryHandler getRouteGeometryHandler) : ControllerBase
{
    /// <summary>
    /// Road distance and driving time (whole 5-minute ticks) between two coordinates,
    /// straight from OSRM - the same call the shipment-insertion flow makes per leg.
    /// Returns 503 when the routing provider is unreachable or has no route.
    /// </summary>
    [HttpGet("routing/leg")]
    public async Task<ActionResult<GetRouteLegResponse>> GetRouteLeg(
        [FromQuery] double fromLat,
        [FromQuery] double fromLng,
        [FromQuery] double toLat,
        [FromQuery] double toLng,
        CancellationToken cancellationToken)
    {
        var response = await getRouteLegHandler.GetRouteLegAsync(
            new GetRouteLegRequest(fromLat, fromLng, toLat, toLng), cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Road distance, driving time, and the road-following polyline between two
    /// coordinates - what a map draws a route line through. Cached per rounded
    /// coordinate pair. Returns 503 when the routing provider is unreachable or has no
    /// route.
    /// </summary>
    [HttpGet("routing/geometry")]
    public async Task<ActionResult<GetRouteGeometryResponse>> GetRouteGeometry(
        [FromQuery] double fromLat,
        [FromQuery] double fromLng,
        [FromQuery] double toLat,
        [FromQuery] double toLng,
        CancellationToken cancellationToken)
    {
        var response = await getRouteGeometryHandler.GetRouteGeometryAsync(
            new GetRouteGeometryRequest(fromLat, fromLng, toLat, toLng), cancellationToken);
        return Ok(response);
    }
}
