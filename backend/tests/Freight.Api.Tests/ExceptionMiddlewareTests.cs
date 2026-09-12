using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Routing.Abstractions;

namespace Freight.Api.Tests;

/// <summary>
/// Program.cs registers one piece of genuinely cross-cutting behavior: an exception
/// middleware mapping RoutingUnavailableException to 503, ArgumentException/
/// InvalidOperationException to 400, with an { error: message } JSON body, and anything
/// else falling through unhandled. Individual controller test files exercise this
/// incidentally via their own "unknown id" cases; this file tests the mapping itself,
/// directly, for each branch.
/// </summary>
public sealed class ExceptionMiddlewareTests : ApiTestBase
{
    [Fact]
    public async Task InvalidOperationException_MapsTo400WithErrorBody()
    {
        // GetTruckingCompanyByIdHandler throws InvalidOperationException for an unknown id.
        var response = await Client.GetAsync($"/companies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Error));
    }

    [Fact]
    public async Task ArgumentException_MapsTo400WithErrorBody()
    {
        // AddTruckHandler -> Truck.Create throws ArgumentException for a blank truck name.
        var response = await Client.PostAsJsonAsync("/trucks", new
        {
            TruckName = "",
            TruckType = TruckType.Refrigerated,
            TruckSize = TruckSize.Medium
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Error));
    }

    [Fact]
    public async Task RoutingUnavailableException_MapsTo503WithErrorBody()
    {
        Factory.RoutingService.ThrowOnCall = new RoutingUnavailableException("OSRM unreachable");

        var response = await Client.GetAsync("/routing/leg?fromLat=52.52&fromLng=13.405&toLat=48.1351&toLng=11.582");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("OSRM unreachable", body.Error);
    }

    private sealed record ErrorBody(string Error);
}
