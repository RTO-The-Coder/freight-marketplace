using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Routing;

public sealed record GetRouteLegRequest(double FromLatitude, double FromLongitude, double ToLatitude, double ToLongitude);

public sealed record GetRouteLegResponse(double DistanceKm, int TimeTicks);

/// <summary>
/// Thin query over <see cref="IRoutingService"/>: the road distance and driving time
/// (whole 5-minute ticks) between two coordinates, straight from OSRM. Exposed via
/// <c>GET /routing/leg</c> as a verification endpoint - the same routing call the
/// shipment-insertion flow makes per leg, callable on its own to check OSRM connectivity
/// and sanity-check the figures.
/// </summary>
public sealed class GetRouteLegHandler(IRoutingService routingService)
{
    public async Task<GetRouteLegResponse> GetRouteLegAsync(GetRouteLegRequest request, CancellationToken cancellationToken = default)
    {
        var from = GeoLocation.Create(request.FromLatitude, request.FromLongitude);
        var to = GeoLocation.Create(request.ToLatitude, request.ToLongitude);

        var leg = await routingService.GetRouteAsync(from, to, cancellationToken);

        return new GetRouteLegResponse(leg.DistanceKm, leg.TimeTicks);
    }
}
