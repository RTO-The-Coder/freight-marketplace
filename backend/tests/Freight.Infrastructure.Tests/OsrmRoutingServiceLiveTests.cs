using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Routing;

namespace Freight.Infrastructure.Tests;

/// <summary>
/// Hits the real public OSRM demo server. Excluded from the default run
/// (<c>Category=Live</c>) because it needs network access and is subject to the demo
/// server's rate limit / uptime. Run explicitly with:
/// <c>dotnet test --filter Category=Live</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class OsrmRoutingServiceLiveTests
{
    private static OsrmRoutingService NewService()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://router.project-osrm.org/"),
            Timeout = TimeSpan.FromSeconds(15),
        };
        return new OsrmRoutingService(httpClient);
    }

    [Fact]
    public async Task GetRouteAsync_BerlinToMunich_ReturnsPlausibleRoadDistanceAndTime()
    {
        var berlin = GeoLocation.Create(52.5200, 13.4050);
        var munich = GeoLocation.Create(48.1351, 11.5820);

        var leg = await NewService().GetRouteAsync(berlin, munich);

        // Road route Berlin -> Munich is ~585 km and ~5.5-6.5 h of driving.
        Assert.InRange(leg.DistanceKm, 500, 700);
        Assert.InRange(leg.TimeTicks, 60, 100); // 60 ticks = 5h, 100 ticks = 8h20m

        // Ticks are always rounded up from the raw duration - never zero for a real leg.
        Assert.True(leg.TimeTicks > 0);
    }

    [Fact]
    public async Task GetRouteAsync_SamePoint_ReturnsNearZeroLeg()
    {
        var point = GeoLocation.Create(52.5200, 13.4050);

        var leg = await NewService().GetRouteAsync(point, point);

        Assert.InRange(leg.DistanceKm, 0, 1);
        Assert.InRange(leg.TimeTicks, 0, 1);
    }
}
