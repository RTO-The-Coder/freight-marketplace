using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Api.Tests.TestSupport;

/// <summary>
/// Deterministic <see cref="IRoutingService"/> test double, registered in place of the
/// real OSRM-backed service (see <see cref="ApiTestFactory"/>) so these HTTP-level tests
/// don't depend on a live third-party routing provider or its ~1 req/sec throttle.
/// Mirrors Freight.Application.Tests' FakeRoutingService.
/// </summary>
public sealed class FakeRoutingService : IRoutingService
{
    public RouteLeg DefaultLeg { get; set; } = new(DistanceKm: 20, TimeTicks: 6);

    /// <summary>When set, every call throws this instead of returning a leg - simulates a routing-provider failure (mapped to HTTP 503 by Program.cs's exception middleware).</summary>
    public RoutingUnavailableException? ThrowOnCall { get; set; }

    public Task<RouteLeg> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        if (ThrowOnCall is not null)
        {
            throw ThrowOnCall;
        }
        return Task.FromResult(DefaultLeg);
    }

    public Task<RouteGeometry> GetRouteGeometryAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        if (ThrowOnCall is not null)
        {
            throw ThrowOnCall;
        }
        return Task.FromResult(new RouteGeometry(DefaultLeg.DistanceKm, DefaultLeg.TimeTicks, Path: [new GeoPoint(from.Latitude, from.Longitude), new GeoPoint(to.Latitude, to.Longitude)]));
    }
}
