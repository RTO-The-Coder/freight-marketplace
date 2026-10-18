using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Geocoding;
using Freight.Domain.Geocoding.Abstractions;

namespace Freight.Api.Tests.Controllers;

public sealed class GeocodingControllerTests : ApiTestBase
{
    [Fact]
    public async Task Search_PostcodeAndSuburb_Returns200WithNamedMatches()
    {
        var response = await Client.GetAsync("/geocoding/search?postcode=1100&suburb=Favoriten");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SearchAreasResponse>(JsonOptions);
        var match = Assert.Single(body!.Matches);
        Assert.Equal("1100 Favoriten, Vienna", match.AreaName);
        Assert.Equal("Austria", match.Country);
        Assert.Equal(["1100 Favoriten"], Factory.GeocodingService.Queries);
    }

    [Fact]
    public async Task Search_BlankSuburb_Returns400ViaExceptionMiddleware()
    {
        var response = await Client.GetAsync("/geocoding/search?postcode=1100&suburb=%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Factory.GeocodingService.Queries);
    }

    [Fact]
    public async Task Search_ProviderUnavailable_Returns503WithItsMessage()
    {
        Factory.GeocodingService.ThrowOnCall = new GeocodingUnavailableException("Nominatim returned 503 Service Unavailable.");

        var response = await Client.GetAsync("/geocoding/search?postcode=1100&suburb=Favoriten");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Nominatim returned 503", await response.Content.ReadAsStringAsync());
    }
}
