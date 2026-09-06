using Freight.Domain.ValueObjects;

namespace Freight.Domain.Routing.Abstractions;

/// <summary>
/// Road distance and driving time between two coordinates - the single external
/// dependency behind every real distance/time figure in Phase 1 (ADR 0011): the ETA
/// calculator, insertion-feasibility check, and later the dispatcher queries and matching
/// engine all resolve their legs through this. Domain/Application depend on the interface,
/// Infrastructure implements it against OSRM. A failed or unroutable call throws
/// <see cref="RoutingUnavailableException"/> - no haversine or placeholder fallback,
/// since a guessed distance would look right while being wrong.
/// </summary>
public interface IRoutingService
{
    /// <summary>The road leg from <paramref name="from"/> to <paramref name="to"/>.</summary>
    /// <exception cref="RoutingUnavailableException">The provider was unreachable, timed out, errored, or found no route.</exception>
    Task<RouteLeg> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default);

    /// <summary>
    /// The road leg from <paramref name="from"/> to <paramref name="to"/> plus its
    /// road-following geometry - the point list a map draws a polyline through. Heavier
    /// than <see cref="GetRouteAsync"/> (asks the provider for the route shape); callers
    /// that only need distance/time use that instead.
    /// </summary>
    /// <exception cref="RoutingUnavailableException">The provider was unreachable, timed out, errored, or found no route.</exception>
    Task<RouteGeometry> GetRouteGeometryAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default);
}

/// <summary>
/// One road leg's distance and driving time. <see cref="TimeTicks"/> is whole 5-minute
/// ticks (the unit every stored leg time uses - see <c>Stop.IncomingLegTimeTick</c>),
/// always rounded UP from the provider's raw duration so a forward route walk that only
/// adds these never produces an optimistic ETA.
/// </summary>
public sealed record RouteLeg(double DistanceKm, int TimeTicks);

/// <summary>One point on a road route, as latitude/longitude degrees.</summary>
public sealed record GeoPoint(double Lat, double Lng);

/// <summary>
/// A road leg with its drawable shape - <see cref="DistanceKm"/>/<see cref="TimeTicks"/>
/// as in <see cref="RouteLeg"/>, plus <see cref="Path"/>, the ordered road-following
/// polyline from start to end.
/// </summary>
public sealed record RouteGeometry(double DistanceKm, int TimeTicks, IReadOnlyList<GeoPoint> Path);
