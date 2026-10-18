using System.Net.Http.Headers;
using System.Text.Json;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Geocoding;
using Freight.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace Freight.Seeder.Fixtures;

/// <summary>
/// One-time (or rarely-rerun) tool that calls the real OSRM routing service for every pair
/// in <see cref="LocationPairs.All"/> not already measured, and writes the result to
/// <c>Fixtures/route-pairs.json</c>.
/// Run manually via <c>dotnet run --build-fixture</c> whenever the pair list changes; the
/// ordinary seed run only ever reads the resulting JSON file, never OSRM itself.
///
/// The same step finds every place's postcode and city (ADR 0013) with one Nominatim reverse
/// lookup per place still missing them, identified by the same
/// User-Agent as the API (<see cref="NominatimOptions"/>). An optional contact address can be
/// added with the <c>Geocoding__ContactEmail</c> environment variable (never committed).
/// </summary>
public static class FixtureBuilder
{
    public static async Task RunAsync(string outputPath)
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://router.project-osrm.org"),
            Timeout = TimeSpan.FromSeconds(10),
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("freight-marketplace-seeder", "1.0"));

        var osrmOptions = Options.Create(new OsrmOptions { MinRequestIntervalMilliseconds = 1100 });
        var routingService = new ThrottlingRoutingService(new OsrmRoutingService(httpClient), osrmOptions);

        // Pairs already in the fixture with unchanged coordinates keep their measured
        // distance/time, so adding pairs only calls OSRM for the new ones.
        var existing = File.Exists(outputPath)
            ? (JsonSerializer.Deserialize<List<RoutePairFixture>>(await File.ReadAllTextAsync(outputPath)) ?? [])
                .ToDictionary(fixture => fixture.PairId)
            : [];

        var results = new List<RoutePairFixture>();
        var failures = new List<string>();

        foreach (var pair in LocationPairs.All)
        {
            if (existing.TryGetValue(pair.PairId, out var known)
                && known.PickupLat == pair.Pickup.Lat && known.PickupLon == pair.Pickup.Lon
                && known.DeliveryLat == pair.Delivery.Lat && known.DeliveryLon == pair.Delivery.Lon)
            {
                results.Add(known with { PickupName = pair.Pickup.Name, DeliveryName = pair.Delivery.Name, Tier = pair.Tier.ToString() });
                Console.WriteLine($"Kept {pair.PairId} from the existing fixture.");
                continue;
            }

            Console.WriteLine($"Routing {pair.PairId}: {pair.Pickup.Name} -> {pair.Delivery.Name} ...");
            try
            {
                var pickup = GeoLocation.Create(pair.Pickup.Lat, pair.Pickup.Lon);
                var delivery = GeoLocation.Create(pair.Delivery.Lat, pair.Delivery.Lon);
                var leg = await routingService.GetRouteAsync(pickup, delivery);

                results.Add(new RoutePairFixture(
                    pair.PairId,
                    pair.Pickup.Name, pair.Pickup.Lat, pair.Pickup.Lon,
                    pair.Delivery.Name, pair.Delivery.Lat, pair.Delivery.Lon,
                    pair.Tier.ToString(),
                    Math.Round(leg.DistanceKm, 1),
                    leg.TimeTicks));

                var hours = leg.TimeTicks * 5 / 60.0;
                Console.WriteLine($"  -> {leg.DistanceKm:N0} km, {hours:N1} h driving ({leg.TimeTicks} ticks)");
            }
            catch (Exception ex)
            {
                failures.Add($"{pair.PairId} ({pair.Pickup.Name} -> {pair.Delivery.Name}): {ex.Message}");
                Console.WriteLine($"  FAILED: {ex.Message}");
            }
        }

        results = await AddAreasAsync(results, failures);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json);

        Console.WriteLine();
        Console.WriteLine($"Wrote {results.Count} route pairs to {outputPath}.");

        if (failures.Count > 0)
        {
            Console.WriteLine($"{failures.Count} problem(s) - fix them (in LocationPairs.cs or the environment) and re-run:");
            foreach (var failure in failures)
            {
                Console.WriteLine($"  - {failure}");
            }
        }
    }

    /// <summary>
    /// Fills each pair's pickup/delivery postcode and city: a place already known in any pair
    /// keeps them, every other place is looked up once (throttled to Nominatim's 1 request/second).
    /// </summary>
    private static async Task<List<RoutePairFixture>> AddAreasAsync(List<RoutePairFixture> pairs, List<string> failures)
    {
        var areas = new Dictionary<string, (string? Postcode, string? City)>();
        foreach (var pair in pairs)
        {
            if (!string.IsNullOrWhiteSpace(pair.PickupCity))
            {
                areas.TryAdd(pair.PickupName, (pair.PickupPostcode, pair.PickupCity));
            }

            if (!string.IsNullOrWhiteSpace(pair.DeliveryCity))
            {
                areas.TryAdd(pair.DeliveryName, (pair.DeliveryPostcode, pair.DeliveryCity));
            }
        }

        var missing = pairs
            .SelectMany(pair => new[] { new NamedPlace(pair.PickupName, pair.PickupLat, pair.PickupLon), new NamedPlace(pair.DeliveryName, pair.DeliveryLat, pair.DeliveryLon) })
            .DistinctBy(place => place.Name)
            .Where(place => !areas.ContainsKey(place.Name))
            .ToList();

        if (missing.Count > 0)
        {
            var options = new NominatimOptions { ContactEmail = Environment.GetEnvironmentVariable("Geocoding__ContactEmail") };
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            };
            var geocoding = new NominatimGeocodingService(httpClient, Options.Create(options));

            foreach (var place in missing)
            {
                Console.WriteLine($"Naming {place.Name} ...");
                try
                {
                    var match = await geocoding.ReverseAsync(place.Lat, place.Lon);
                    if (match is null || match.City.Length == 0)
                    {
                        failures.Add($"{place.Name}: Nominatim found no city at ({place.Lat}, {place.Lon}).");
                    }
                    else
                    {
                        areas[place.Name] = (match.Postcode, match.City);
                        Console.WriteLine($"  -> {match.Postcode} {match.City} ({match.Country})");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{place.Name}: {ex.Message}");
                    Console.WriteLine($"  FAILED: {ex.Message}");
                }

                await Task.Delay(options.MinRequestIntervalMilliseconds);
            }
        }

        return pairs
            .Select(pair =>
            {
                var pickup = areas.GetValueOrDefault(pair.PickupName);
                var delivery = areas.GetValueOrDefault(pair.DeliveryName);
                return pair with
                {
                    PickupPostcode = pickup.Postcode,
                    PickupCity = pickup.City,
                    DeliveryPostcode = delivery.Postcode,
                    DeliveryCity = delivery.City,
                };
            })
            .ToList();
    }
}
