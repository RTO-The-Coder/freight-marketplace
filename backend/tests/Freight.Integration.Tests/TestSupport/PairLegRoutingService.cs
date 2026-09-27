using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Integration.Tests.TestSupport;

/// <summary>
/// Stand-in for OSRM: answers every routing call from a fixed (from, to) -> leg table that
/// the scenario defines, so each hop's drive time is chosen by the test and the expected
/// EU-561 outcome can be worked out by hand. One assignment asks for up to five legs in an
/// order that depends on the insertion position, which is why this is keyed by pair rather
/// than handing out "the next leg".
///
/// Read-only after construction (nothing is recorded), so it can't carry state between
/// tests. A pair missing from the table throws instead of defaulting, so an unplanned hop
/// fails the test at the call that asked for it.
/// </summary>
public sealed class PairLegRoutingService : IRoutingService
{
    // Points go through JSON and Postgres before the backend asks about them; rounding to
    // ~10 cm makes the lookup immune to any floating-point drift on that round trip.
    private const int CoordinateDecimals = 6;

    private readonly IReadOnlyDictionary<PointPair, RouteLeg> _legs;

    public PairLegRoutingService(IEnumerable<(GeoLocation From, GeoLocation To, RouteLeg Leg)> legs)
    {
        var table = new Dictionary<PointPair, RouteLeg>();

        foreach (var (from, to, leg) in legs)
        {
            if (!table.TryAdd(PointPair.Of(from, to), leg))
            {
                throw new ArgumentException($"The leg {Describe(from)} -> {Describe(to)} is defined twice.", nameof(legs));
            }
        }

        _legs = table;
    }

    public Task<RouteLeg> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default) =>
        Task.FromResult(Lookup(from, to));

    public Task<RouteGeometry> GetRouteGeometryAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        var leg = Lookup(from, to);
        return Task.FromResult(new RouteGeometry(
            leg.DistanceKm,
            leg.TimeTicks,
            Path: [new GeoPoint(from.Latitude, from.Longitude), new GeoPoint(to.Latitude, to.Longitude)]));
    }

    private RouteLeg Lookup(GeoLocation from, GeoLocation to) =>
        _legs.TryGetValue(PointPair.Of(from, to), out var leg)
            ? leg
            // Directional on purpose: A -> B does not imply B -> A. Surfaces as a 503 from
            // the API, with this message in the error body.
            : throw new RoutingUnavailableException(
                $"No leg defined for {Describe(from)} -> {Describe(to)}. Add it to the scenario's route table.");

    private static string Describe(GeoLocation point) =>
        $"({Math.Round(point.Latitude, CoordinateDecimals)}, {Math.Round(point.Longitude, CoordinateDecimals)})";

    private readonly record struct PointPair(double FromLat, double FromLng, double ToLat, double ToLng)
    {
        public static PointPair Of(GeoLocation from, GeoLocation to) => new(
            Math.Round(from.Latitude, CoordinateDecimals),
            Math.Round(from.Longitude, CoordinateDecimals),
            Math.Round(to.Latitude, CoordinateDecimals),
            Math.Round(to.Longitude, CoordinateDecimals));
    }
}
