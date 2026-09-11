using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Tests;

/// <summary>
/// Deterministic <see cref="IRoutingService"/> test double. Records every call (so tests
/// can assert exactly which legs were measured, and how many times) and returns a fixed
/// leg by default - override <see cref="LegFor"/> to vary the response per from/to pair,
/// or set <see cref="ThrowOnCall"/> to simulate an unroutable/unreachable provider.
/// </summary>
public sealed class FakeRoutingService : IRoutingService
{
    public sealed record Call(GeoLocation From, GeoLocation To);

    public List<Call> Requests { get; } = [];

    /// <summary>Default leg returned when <see cref="LegFor"/> has no override for the requested pair.</summary>
    public RouteLeg DefaultLeg { get; set; } = new(DistanceKm: 20, TimeTicks: 6);

    /// <summary>Optional per-pair override, keyed by (From, To) reference equality is not required - lookup uses value equality on the records' coordinates.</summary>
    public Dictionary<(GeoLocation From, GeoLocation To), RouteLeg> LegFor { get; } = [];

    /// <summary>When set, every call throws this instead of returning a leg - simulates a routing-provider failure.</summary>
    public RoutingUnavailableException? ThrowOnCall { get; set; }

    public Task<RouteLeg> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        Requests.Add(new Call(from, to));

        if (ThrowOnCall is not null)
        {
            throw ThrowOnCall;
        }

        var leg = LegFor.TryGetValue((from, to), out var overriddenLeg) ? overriddenLeg : DefaultLeg;
        return Task.FromResult(leg);
    }

    public Task<RouteGeometry> GetRouteGeometryAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        Requests.Add(new Call(from, to));

        if (ThrowOnCall is not null)
        {
            throw ThrowOnCall;
        }

        var leg = LegFor.TryGetValue((from, to), out var overriddenLeg) ? overriddenLeg : DefaultLeg;
        return Task.FromResult(new RouteGeometry(leg.DistanceKm, leg.TimeTicks, Path: [new GeoPoint(from.Latitude, from.Longitude), new GeoPoint(to.Latitude, to.Longitude)]));
    }
}
