using System.Net;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Tests;

/// <summary>
/// <see cref="OsrmRoutingService"/> against a stubbed transport - no network. Covers the
/// response parse, the always-round-up tick conversion, and every failure mode collapsing
/// to <see cref="RoutingUnavailableException"/>, plus the retry of temporary server errors
/// (run with zero delays so the tests don't wait).
/// </summary>
public sealed class OsrmRoutingServiceTests
{
    private static readonly GeoLocation From = GeoLocation.Create(52.52, 13.405);
    private static readonly GeoLocation To = GeoLocation.Create(48.135, 11.582);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private static OsrmRoutingService NewService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://osrm.test/") },
            Options.Create(new OsrmOptions { MinRequestIntervalMilliseconds = 0, RetryDelayMilliseconds = 0 }));

    /// <summary>Answers each call with the next responder in turn (the last one repeats).</summary>
    private static StubHandler Sequence(params Func<HttpResponseMessage>[] responders)
    {
        var next = 0;
        return new StubHandler(_ => responders[Math.Min(next++, responders.Length - 1)]());
    }

    private static HttpResponseMessage OkRoute() =>
        Json(HttpStatusCode.OK, """{"code":"Ok","routes":[{"distance":1000,"duration":900}]}""");

    private static HttpResponseMessage BadGateway() => new(HttpStatusCode.BadGateway);

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

    [Fact]
    public async Task GetRouteAsync_BadGatewayThenOk_RetriesAndSucceeds()
    {
        var handler = Sequence(BadGateway, OkRoute);

        var leg = await NewService(handler).GetRouteAsync(From, To);

        Assert.Equal(3, leg.TimeTicks);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetRouteAsync_ThreeBadGateways_ThrowsRoutingUnavailableAfterThreeAttempts()
    {
        var handler = Sequence(BadGateway);

        await Assert.ThrowsAsync<RoutingUnavailableException>(() => NewService(handler).GetRouteAsync(From, To));
        Assert.Equal(3, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task GetRouteAsync_OtherTemporaryStatusThenOk_RetriesAndSucceeds(HttpStatusCode status)
    {
        var handler = Sequence(() => new HttpResponseMessage(status), OkRoute);

        await NewService(handler).GetRouteAsync(From, To);

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetRouteAsync_ConnectionErrorThenOk_RetriesAndSucceeds()
    {
        var handler = Sequence(() => throw new HttpRequestException("connection reset"), OkRoute);

        await NewService(handler).GetRouteAsync(From, To);

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetRouteAsync_BadRequest_IsNotRetried()
    {
        var handler = Sequence(() => new HttpResponseMessage(HttpStatusCode.BadRequest), OkRoute);

        await Assert.ThrowsAsync<RoutingUnavailableException>(() => NewService(handler).GetRouteAsync(From, To));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetRouteAsync_NoRoute_IsNotRetried()
    {
        var handler = Sequence(() => Json(HttpStatusCode.OK, """{"code":"NoRoute","routes":[]}"""), OkRoute);

        await Assert.ThrowsAsync<RoutingUnavailableException>(() => NewService(handler).GetRouteAsync(From, To));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetRouteGeometryAsync_BadGatewayThenOk_RetriesAndSucceeds()
    {
        var handler = Sequence(BadGateway, () => Json(HttpStatusCode.OK,
            """{"code":"Ok","routes":[{"distance":1000,"duration":900,"geometry":{"coordinates":[[13.405,52.52],[11.582,48.135]]}}]}"""));

        var route = await NewService(handler).GetRouteGeometryAsync(From, To);

        Assert.Equal(2, route.Path.Count);
        Assert.Equal(2, handler.Calls);
    }
}
