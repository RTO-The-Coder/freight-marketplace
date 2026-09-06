using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Tests;

/// <summary>
/// An <see cref="IRoutingService"/> test double that returns a fixed leg for every call
/// (default 650 km / 78 ticks - the placeholder figures the handler used before OSRM was
/// wired, so existing assertions on leg distance/time stay valid) and records the
/// from/to coordinate pairs it was asked about, so a test can assert which legs the
/// handler measured for a given insertion.
/// </summary>
internal sealed class FakeRoutingService : IRoutingService
{
    private readonly RouteLeg _leg;

    public FakeRoutingService(double distanceKm = 650, int timeTicks = 78)
    {
        _leg = new RouteLeg(distanceKm, timeTicks);
    }

    public List<(GeoLocation From, GeoLocation To)> Requests { get; } = [];

    /// <summary>When set, thrown instead of returning a leg - to exercise the abort path.</summary>
    public Exception? ThrowOnCall { get; set; }

    public Task<RouteLeg> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        Requests.Add((from, to));

        if (ThrowOnCall is not null)
        {
            throw ThrowOnCall;
        }

        return Task.FromResult(_leg);
    }

    public Task<RouteGeometry> GetRouteGeometryAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        Requests.Add((from, to));

        if (ThrowOnCall is not null)
        {
            throw ThrowOnCall;
        }

        // A trivial two-point path from start to end - enough for callers that just need
        // a non-empty geometry.
        var path = new List<GeoPoint>
        {
            new(from.Latitude, from.Longitude),
            new(to.Latitude, to.Longitude),
        };
        return Task.FromResult(new RouteGeometry(_leg.DistanceKm, _leg.TimeTicks, path));
    }
}
