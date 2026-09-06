using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;

namespace Freight.Application.Routing;

public sealed record GetRouteGeometryRequest(double FromLatitude, double FromLongitude, double ToLatitude, double ToLongitude);

public sealed record RoutePointDto(double Lat, double Lng);

public sealed record GetRouteGeometryResponse(double DistanceKm, int TimeTicks, IReadOnlyList<RoutePointDto> Path);

/// <summary>
/// The road distance, driving time (whole 5-minute ticks), and drawable road-following
/// polyline between two coordinates, from OSRM. Exposed via <c>GET /routing/geometry</c>
/// - the map layer calls it once per consecutive stop pair to draw a trip.
///
/// Results are cached in-process keyed on the coordinates rounded to ~11 m: a multi-stop
/// trip map is N-1 calls otherwise, each spaced ~1.1s by the routing throttle, and the
/// seed data's suburb-centroid coordinates repeat heavily across trips.
/// </summary>
public sealed class GetRouteGeometryHandler(IRoutingService routingService, IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public async Task<GetRouteGeometryResponse> GetRouteGeometryAsync(GetRouteGeometryRequest request, CancellationToken cancellationToken = default)
    {
        var from = GeoLocation.Create(request.FromLatitude, request.FromLongitude);
        var to = GeoLocation.Create(request.ToLatitude, request.ToLongitude);

        var key = $"routegeom:{Round(from.Latitude)},{Round(from.Longitude)}=>{Round(to.Latitude)},{Round(to.Longitude)}";

        if (cache.TryGetValue(key, out GetRouteGeometryResponse? cached) && cached is not null)
        {
            return cached;
        }

        var geometry = await routingService.GetRouteGeometryAsync(from, to, cancellationToken);

        var response = new GetRouteGeometryResponse(
            geometry.DistanceKm,
            geometry.TimeTicks,
            geometry.Path.Select(point => new RoutePointDto(point.Lat, point.Lng)).ToList());

        cache.Set(key, response, CacheDuration);
        return response;
    }

    private static double Round(double coordinate) => Math.Round(coordinate, 4);
}
