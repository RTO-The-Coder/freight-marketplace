namespace Freight.Seeder.Fixtures;

/// <summary>
/// One <see cref="LocationPairs"/> entry plus its real, OSRM-computed road distance/time -
/// exactly what <c>route-pairs.json</c> holds. Built once via <c>--build-fixture</c> and
/// committed to the repo so the ordinary seed run never calls OSRM itself.
/// </summary>
public sealed record RoutePairFixture(
    string PairId,
    string PickupName,
    double PickupLat,
    double PickupLon,
    string DeliveryName,
    double DeliveryLat,
    double DeliveryLon,
    string Tier,
    double DistanceKm,
    int TimeTicks);
