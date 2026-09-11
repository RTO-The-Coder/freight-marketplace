using Freight.Application.Routing;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Tests.Routing;

public sealed class GetRouteLegHandlerTests
{
    [Fact]
    public async Task GetRouteLegAsync_BuildsFromAndToFromRequestCoordinates()
    {
        var routingService = new FakeRoutingService();
        var handler = new GetRouteLegHandler(routingService);

        await handler.GetRouteLegAsync(new GetRouteLegRequest(52.52, 13.405, 48.1351, 11.582));

        var call = Assert.Single(routingService.Requests);
        Assert.Equal(GeoLocation.Create(52.52, 13.405), call.From);
        Assert.Equal(GeoLocation.Create(48.1351, 11.582), call.To);
    }

    [Fact]
    public async Task GetRouteLegAsync_ReturnsDistanceAndTimeTicksUnmodified()
    {
        var routingService = new FakeRoutingService { DefaultLeg = new RouteLeg(DistanceKm: 504.3, TimeTicks: 72) };
        var handler = new GetRouteLegHandler(routingService);

        var response = await handler.GetRouteLegAsync(new GetRouteLegRequest(52.52, 13.405, 48.1351, 11.582));

        Assert.Equal(504.3, response.DistanceKm);
        Assert.Equal(72, response.TimeTicks);
    }

    [Fact]
    public async Task GetRouteLegAsync_RoutingServiceThrows_PropagatesUnwrapped()
    {
        var routingService = new FakeRoutingService { ThrowOnCall = new RoutingUnavailableException("OSRM unreachable") };
        var handler = new GetRouteLegHandler(routingService);

        await Assert.ThrowsAsync<RoutingUnavailableException>(() =>
            handler.GetRouteLegAsync(new GetRouteLegRequest(52.52, 13.405, 48.1351, 11.582)));
    }
}
