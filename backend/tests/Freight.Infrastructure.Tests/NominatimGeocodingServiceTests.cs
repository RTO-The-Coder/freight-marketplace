using System.Net;
using Freight.Domain.Geocoding.Abstractions;
using Freight.Infrastructure.Geocoding;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Tests;

/// <summary>
/// <see cref="NominatimGeocodingService"/> against a stubbed transport - no network. Covers the
/// request (query, parameters, identifying User-Agent), reading postcode/city/country, and every
/// failure mode collapsing to <see cref="GeocodingUnavailableException"/>.
/// </summary>
public sealed class NominatimGeocodingServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static NominatimGeocodingService NewService(StubHandler handler, string? contactEmail = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") },
            Options.Create(new NominatimOptions { ContactEmail = contactEmail }));

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private const string FavoritenResult = """
        [{"lat":"48.1550","lon":"16.3820","display_name":"Favoriten, Vienna, 1100, Austria",
          "address":{"road":"Somestreet","house_number":"12","suburb":"Katastralgemeinde Favoriten",
                     "city":"Vienna","postcode":"1100","country":"Austria"}}]
        """;

    [Fact]
    public async Task SearchAsync_ReadsCoordinatesPostcodeCityAndCountry()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, FavoritenResult));

        var match = Assert.Single(await NewService(handler).SearchAsync("1100 Favoriten"));

        Assert.Equal(new GeocodingMatch("1100", "Vienna", "Austria", 48.155, 16.382), match);
    }

    [Fact]
    public async Task SearchAsync_NoCity_FallsBackToTownVillageOrCounty()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """[{"lat":"50","lon":"9","address":{"village":"Kleindorf","postcode":"63000","country":"Germany"}}]"""));

        var match = Assert.Single(await NewService(handler).SearchAsync("63000 Kleindorf"));

        Assert.Equal("Kleindorf", match.City);
    }

    [Fact]
    public async Task SearchAsync_SendsTheQueryWithEnglishNamesAndAddressDetails()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));

        await NewService(handler).SearchAsync(" 1100 Favoriten ");

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.StartsWith("https://nominatim.test/search?q=1100%20Favoriten&", uri);
        Assert.Contains("format=jsonv2", uri);
        Assert.Contains("addressdetails=1", uri);
        Assert.Contains("accept-language=en", uri);
        Assert.Contains("limit=5", uri);
    }

    [Fact]
    public async Task SearchAsync_IdentifiesTheAppByNameAndProjectPage()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));

        await NewService(handler).SearchAsync("1100 Favoriten");

        var userAgent = handler.LastRequest!.Headers.UserAgent.ToString();
        Assert.Contains("freight-marketplace/1.0", userAgent);
        Assert.Contains("https://github.com/RTO-The-Coder/freight-marketplace", userAgent);
    }

    [Fact]
    public async Task SearchAsync_ContactEmailConfigured_AddsItToTheUserAgent()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));

        await NewService(handler, contactEmail: "dev@example.com").SearchAsync("1100 Favoriten");

        Assert.Contains("dev@example.com", handler.LastRequest!.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmptyList()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));

        Assert.Empty(await NewService(handler).SearchAsync("99999 Nowhere"));
    }

    [Fact]
    public async Task SearchAsync_ResultWithoutUsableCoordinates_IsSkipped()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """[{"lat":"not-a-number","lon":"16.38","address":{"city":"Vienna"}}]"""));

        Assert.Empty(await NewService(handler).SearchAsync("1100 Favoriten"));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SearchAsync_ErrorStatus_ThrowsGeocodingUnavailable(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));

        await Assert.ThrowsAsync<GeocodingUnavailableException>(() => NewService(handler).SearchAsync("1100 Favoriten"));
    }

    [Fact]
    public async Task SearchAsync_Unreachable_ThrowsGeocodingUnavailable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("no route to host"));

        await Assert.ThrowsAsync<GeocodingUnavailableException>(() => NewService(handler).SearchAsync("1100 Favoriten"));
    }

    [Fact]
    public async Task SearchAsync_UnreadableBody_ThrowsGeocodingUnavailable()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "<html>maintenance</html>"));

        await Assert.ThrowsAsync<GeocodingUnavailableException>(() => NewService(handler).SearchAsync("1100 Favoriten"));
    }

    [Fact]
    public async Task ReverseAsync_ReadsTheAreaAtACoordinate()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, FavoritenResult.Trim().TrimStart('[').TrimEnd(']')));

        var match = await NewService(handler).ReverseAsync(48.155, 16.382);

        Assert.Equal(new GeocodingMatch("1100", "Vienna", "Austria", 48.155, 16.382), match);
        Assert.Contains("reverse?lat=48.155&lon=16.382", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task ReverseAsync_NothingThere_ReturnsNull()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"error":"Unable to geocode"}"""));

        Assert.Null(await NewService(handler).ReverseAsync(0, 0));
    }
}
