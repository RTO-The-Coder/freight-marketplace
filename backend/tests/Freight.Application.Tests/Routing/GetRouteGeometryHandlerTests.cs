using Freight.Application.Routing;
using Microsoft.Extensions.Caching.Memory;

namespace Freight.Application.Tests.Routing;

public sealed class GetRouteGeometryHandlerTests
{
    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task GetRouteGeometryAsync_CacheMiss_CallsRoutingServiceOnceAndPopulatesCache()
    {
        var routingService = new FakeRoutingService();
        var handler = new GetRouteGeometryHandler(routingService, NewCache());

        var response = await handler.GetRouteGeometryAsync(new GetRouteGeometryRequest(52.52, 13.405, 48.1351, 11.582));

        Assert.Single(routingService.Requests);
        Assert.Equal(routingService.DefaultLeg.DistanceKm, response.DistanceKm);
        Assert.Equal(routingService.DefaultLeg.TimeTicks, response.TimeTicks);
        Assert.Equal(2, response.Path.Count);
    }

    [Fact]
    public async Task GetRouteGeometryAsync_SecondCallSameCoordinates_ServesFromCache_NoSecondRoutingCall()
    {
        var routingService = new FakeRoutingService();
        var cache = NewCache();
        var handler = new GetRouteGeometryHandler(routingService, cache);
        var request = new GetRouteGeometryRequest(52.52, 13.405, 48.1351, 11.582);

        var first = await handler.GetRouteGeometryAsync(request);
        var second = await handler.GetRouteGeometryAsync(request);

        Assert.Single(routingService.Requests);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetRouteGeometryAsync_CoordinatesDifferOnlyPastFourthDecimal_SharesSameCacheEntry()
    {
        var routingService = new FakeRoutingService();
        var cache = NewCache();
        var handler = new GetRouteGeometryHandler(routingService, cache);

        // 52.520000 vs 52.5200004 - differ only at the 7th decimal, rounds to the same
        // 4-decimal-place key (~11m precision).
        await handler.GetRouteGeometryAsync(new GetRouteGeometryRequest(52.520000, 13.405, 48.1351, 11.582));
        await handler.GetRouteGeometryAsync(new GetRouteGeometryRequest(52.5200004, 13.405, 48.1351, 11.582));

        Assert.Single(routingService.Requests);
    }

    [Fact]
    public async Task GetRouteGeometryAsync_CoordinatesDifferAtFourthDecimal_IsACacheMiss()
    {
        var routingService = new FakeRoutingService();
        var cache = NewCache();
        var handler = new GetRouteGeometryHandler(routingService, cache);

        await handler.GetRouteGeometryAsync(new GetRouteGeometryRequest(52.5200, 13.405, 48.1351, 11.582));
        await handler.GetRouteGeometryAsync(new GetRouteGeometryRequest(52.5201, 13.405, 48.1351, 11.582));

        Assert.Equal(2, routingService.Requests.Count);
    }

    [Fact]
    public async Task GetRouteGeometryAsync_RoutingServiceThrows_PropagatesUnwrapped_NothingCached()
    {
        var routingService = new FakeRoutingService { ThrowOnCall = new Freight.Domain.Routing.Abstractions.RoutingUnavailableException("down") };
        var cache = NewCache();
        var handler = new GetRouteGeometryHandler(routingService, cache);

        await Assert.ThrowsAsync<Freight.Domain.Routing.Abstractions.RoutingUnavailableException>(() =>
            handler.GetRouteGeometryAsync(new GetRouteGeometryRequest(52.52, 13.405, 48.1351, 11.582)));
    }
}
