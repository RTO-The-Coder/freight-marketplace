using System.Net.Http.Headers;
using System.Text.Json;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace Freight.Seeder.Fixtures;

/// <summary>
/// One-time (or rarely-rerun) tool that calls the real OSRM routing service for every pair
/// in <see cref="LocationPairs.All"/> and writes the result to <c>Fixtures/route-pairs.json</c>.
/// Run manually via <c>dotnet run --build-fixture</c> whenever the pair list changes; the
/// ordinary seed run only ever reads the resulting JSON file, never OSRM itself.
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

        var results = new List<RoutePairFixture>();
        var failures = new List<string>();

        foreach (var pair in LocationPairs.All)
        {
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

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json);

        Console.WriteLine();
        Console.WriteLine($"Wrote {results.Count} route pairs to {outputPath}.");

        if (failures.Count > 0)
        {
            Console.WriteLine($"{failures.Count} pair(s) failed to route (removed from the fixture - fix or replace them in LocationPairs.cs and re-run):");
            foreach (var failure in failures)
            {
                Console.WriteLine($"  - {failure}");
            }
        }
    }
}
