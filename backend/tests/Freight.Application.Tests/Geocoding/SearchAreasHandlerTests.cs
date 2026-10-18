using Freight.Application.Geocoding;
using Freight.Domain.Geocoding.Abstractions;
using Moq;

namespace Freight.Application.Tests.Geocoding;

public sealed class SearchAreasHandlerTests
{
    private static (SearchAreasHandler Handler, Mock<IGeocodingService> Geocoding) NewHandler(params GeocodingMatch[] matches)
    {
        var geocoding = new Mock<IGeocodingService>();
        geocoding.Setup(g => g.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(matches);
        return (new SearchAreasHandler(geocoding.Object), geocoding);
    }

    [Fact]
    public async Task SearchAreasAsync_SearchesPostcodeAndSuburbOnly()
    {
        var (handler, geocoding) = NewHandler();

        await handler.SearchAreasAsync(new SearchAreasRequest(" 1100 ", " Favoriten "));

        geocoding.Verify(g => g.SearchAsync("1100 Favoriten", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAreasAsync_NamesEachMatchByTheTypedSuburbAndTheMatchedCity()
    {
        var (handler, _) = NewHandler(
            new GeocodingMatch("1100", "Vienna", "Austria", 48.155, 16.382),
            new GeocodingMatch("1100", "Favoriten-Nord", "Germany", 50.0, 9.0));

        var response = await handler.SearchAreasAsync(new SearchAreasRequest("1100", "Favoriten"));

        Assert.Collection(
            response.Matches,
            match =>
            {
                Assert.Equal("1100 Favoriten, Vienna", match.AreaName);
                Assert.Equal("Austria", match.Country);
                Assert.Equal(48.155, match.Latitude);
                Assert.Equal(16.382, match.Longitude);
            },
            match => Assert.Equal("1100 Favoriten, Favoriten-Nord", match.AreaName));
    }

    [Fact]
    public async Task SearchAreasAsync_MatchesThatReadTheSame_AreListedOnce()
    {
        var (handler, _) = NewHandler(
            new GeocodingMatch("1100", "Vienna", "Austria", 48.155, 16.382),
            new GeocodingMatch("1100", "Vienna", "Austria", 48.170, 16.390));

        var response = await handler.SearchAreasAsync(new SearchAreasRequest("1100", "Favoriten"));

        Assert.Equal(48.155, Assert.Single(response.Matches).Latitude);
    }

    [Fact]
    public async Task SearchAreasAsync_NoMatches_ReturnsEmptyList()
    {
        var (handler, _) = NewHandler();

        var response = await handler.SearchAreasAsync(new SearchAreasRequest("99999", "Nowhere"));

        Assert.Empty(response.Matches);
    }

    [Theory]
    [InlineData("", "Favoriten")]
    [InlineData("   ", "Favoriten")]
    [InlineData("1234567890123", "Favoriten")]
    [InlineData("1100", "")]
    [InlineData("1100", "   ")]
    public async Task SearchAreasAsync_MissingOrTooLongInput_ThrowsWithoutSearching(string postcode, string suburb)
    {
        var (handler, geocoding) = NewHandler();

        await Assert.ThrowsAsync<ArgumentException>(() => handler.SearchAreasAsync(new SearchAreasRequest(postcode, suburb)));
        geocoding.Verify(g => g.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
