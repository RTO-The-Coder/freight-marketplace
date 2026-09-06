using System.Net;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Routing;

namespace Freight.Infrastructure.Tests;

/// <summary>
/// <see cref="OsrmRoutingService"/> against a stubbed transport - no network. Covers the
/// response parse, the always-round-up tick conversion, and every failure mode collapsing
/// to <see cref="RoutingUnavailableException"/>.
/// </summary>
public sealed class OsrmRoutingServiceTests
{
    private static readonly GeoLocation From = GeoLocation.Create(52.52, 13.405);
    private static readonly GeoLocation To = GeoLocation.Create(48.135, 11.582);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static OsrmRoutingService NewService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://osrm.test/") });

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetRouteAsync_SuccessfulResponse_ParsesDistanceAndRoundsDurationUpToTicks()
    {
        // 585247.7 m -> 585.2477 km; 21204.1 s / 300 = 70.68 -> 71 ticks.
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":"Ok","routes":[{"distance":585247.7,"duration":21204.1}]}"""));

        var leg = await NewService(handler).GetRouteAsync(From, To);

        Assert.Equal(585.2477, leg.DistanceKm, precision: 4);
        Assert.Equal(71, leg.TimeTicks);
    }

    [Fact]
    public async Task GetRouteAsync_DurationExactlyOnTickBoundary_DoesNotRoundUp()
    {
        // 900 s / 300 = 3.0 exactly -> 3 ticks, not 4.
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":"Ok","routes":[{"distance":1000,"duration":900}]}"""));

        var leg = await NewService(handler).GetRouteAsync(From, To);

        Assert.Equal(3, leg.TimeTicks);
    }

    [Fact]
    public async Task GetRouteAsync_RequestsLonLatOrderWithInvariantDecimalSeparators()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":"Ok","routes":[{"distance":1,"duration":1}]}"""));

        await NewService(handler).GetRouteAsync(From, To);

        // lon,lat;lon,lat with '.' separators - never "13,405".
        Assert.Contains("driving/13.405,52.52;11.582,48.135", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetRouteAsync_SendsUserAgentHeader()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"code":"Ok","routes":[{"distance":1,"duration":1}]}"""));

        await NewService(handler).GetRouteAsync(From, To);

        Assert.NotEmpty(handler.LastRequest!.Headers.UserAgent);
    }

    [Fact]
    public async Task GetRouteAsync_NonSuccessStatus_ThrowsRoutingUnavailable()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<RoutingUnavailableException>(() => NewService(handler).GetRouteAsync(From, To));
    }

    [Fact]
    public async Task GetRouteAsync_OsrmReportsNoRoute_ThrowsRoutingUnavailable()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"code":"NoRoute","routes":[]}"""));

        await Assert.ThrowsAsync<RoutingUnavailableException>(() => NewService(handler).GetRouteAsync(From, To));
    }

    [Fact]
    public async Task GetRouteAsync_EmptyRoutesArray_ThrowsRoutingUnavailable()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"code":"Ok","routes":[]}"""));

        await Assert.ThrowsAsync<RoutingUnavailableException>(() => NewService(handler).GetRouteAsync(From, To));
    }

    [Fact]
    public async Task GetRouteAsync_TransportFailure_ThrowsRoutingUnavailable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<RoutingUnavailableException>(() => NewService(handler).GetRouteAsync(From, To));
    }

    [Fact]
    public async Task GetRouteAsync_MalformedJson_ThrowsRoutingUnavailable()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "not json"));

        await Assert.ThrowsAsync<RoutingUnavailableException>(() => NewService(handler).GetRouteAsync(From, To));
    }
}
