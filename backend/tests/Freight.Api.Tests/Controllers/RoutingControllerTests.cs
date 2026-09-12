using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Routing;
using Freight.Domain.Routing.Abstractions;

namespace Freight.Api.Tests.Controllers;

public sealed class RoutingControllerTests : ApiTestBase
{
    [Fact]
    public async Task GetRouteLeg_ValidCoordinates_ReturnsFakeRoutingServiceLeg()
    {
        Factory.RoutingService.DefaultLeg = new RouteLeg(DistanceKm: 42, TimeTicks: 12);

        var response = await Client.GetAsync("/routing/leg?fromLat=52.52&fromLng=13.405&toLat=48.1351&toLng=11.582");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetRouteLegResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(42, body.DistanceKm);
        Assert.Equal(12, body.TimeTicks);
    }

    [Fact]
    public async Task GetRouteGeometry_ValidCoordinates_ReturnsPathWithEndpoints()
    {
        Factory.RoutingService.DefaultLeg = new RouteLeg(DistanceKm: 10, TimeTicks: 3);

        var response = await Client.GetAsync("/routing/geometry?fromLat=52.52&fromLng=13.405&toLat=48.1351&toLng=11.582");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetRouteGeometryResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(10, body.DistanceKm);
        Assert.Equal(2, body.Path.Count);
    }

    [Fact]
    public async Task GetRouteLeg_RoutingProviderUnavailable_Returns503ViaExceptionMiddleware()
    {
        Factory.RoutingService.ThrowOnCall = new RoutingUnavailableException("OSRM unreachable");

        var response = await Client.GetAsync("/routing/leg?fromLat=52.52&fromLng=13.405&toLat=48.1351&toLng=11.582");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
