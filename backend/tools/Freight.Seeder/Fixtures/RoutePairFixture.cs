namespace Freight.Seeder.Fixtures;

/// <summary>
/// One <see cref="LocationPairs"/> entry plus its real, OSRM-computed road distance/time -
/// exactly what <c>route-pairs.json</c> holds. Built once via <c>--build-fixture</c> and
/// committed to the repo so the ordinary seed run never calls OSRM itself.
/// The postcode and city of each place are looked up once on Nominatim by the same step, so
/// seeded addresses and shipments are named like real bookings - "1100 Favoriten, Vienna", with
/// the suburb taken from the place's own name (ADR 0013) - without the seed run calling Nominatim.
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
    int TimeTicks,
    string? PickupPostcode = null,
    string? PickupCity = null,
    string? DeliveryPostcode = null,
    string? DeliveryCity = null);
