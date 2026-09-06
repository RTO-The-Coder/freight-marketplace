using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Api.Tests;

/// <summary>
/// Replaces the real OSRM-backed <see cref="IRoutingService"/> in endpoint tests so the
/// assign-shipment flow never makes a live HTTP call. Returns a fixed 650 km / 78 tick
/// leg for every request - the placeholder figures the handler used before OSRM, so the
/// existing endpoint assertions (stop counts, window feasibility across a ~6.5h leg)
/// stay valid.
/// </summary>
internal sealed class StubRoutingService : IRoutingService
{
    public Task<RouteLeg> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RouteLeg(650, 78));

    public Task<RouteGeometry> GetRouteGeometryAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RouteGeometry(650, 78, new List<GeoPoint>
        {
            new(from.Latitude, from.Longitude),
            new(to.Latitude, to.Longitude),
        }));
}
