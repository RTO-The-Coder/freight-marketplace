using Freight.Domain.Client;
using Freight.Domain.Geocoding.Abstractions;

namespace Freight.Application.Geocoding;

public sealed record SearchAreasRequest(string Postcode, string Suburb);

/// <param name="AreaName">What the saved address will be called, e.g. "1100 Favoriten, Vienna" - see <see cref="ShipperAddress.ComposeAreaName"/>.</param>
public sealed record AreaMatchDto(string AreaName, string Country, double Latitude, double Longitude);

public sealed record SearchAreasResponse(IReadOnlyList<AreaMatchDto> Matches);

/// <summary>
/// The areas matching a postcode and suburb - the "Find area" step of adding a saved address
/// (ADR 0013). Only a postcode and a suburb are ever searched, never a street address, so a
/// saved address is always a generic area. Each match is named by the postcode and suburb as
/// typed plus the city the geocoder places it in; matches that would read the same (same name
/// and country) are listed once. Exposed via <c>GET /geocoding/search</c>; the shipper picks one
/// match and saves it with <c>POST /shippers/{id}/addresses</c>.
/// </summary>
public sealed class SearchAreasHandler(IGeocodingService geocodingService)
{
    public const int MaxPostcodeLength = 12;
    public const int MaxSuburbLength = 100;

    public async Task<SearchAreasResponse> SearchAreasAsync(SearchAreasRequest request, CancellationToken cancellationToken = default)
    {
        var postcode = request.Postcode?.Trim() ?? string.Empty;
        var suburb = request.Suburb?.Trim() ?? string.Empty;

        if (postcode.Length == 0 || postcode.Length > MaxPostcodeLength)
        {
            throw new ArgumentException($"A postcode of 1-{MaxPostcodeLength} characters is required.", nameof(request));
        }

        if (suburb.Length == 0 || suburb.Length > MaxSuburbLength)
        {
            throw new ArgumentException($"A suburb of 1-{MaxSuburbLength} characters is required.", nameof(request));
        }

        var matches = await geocodingService.SearchAsync($"{postcode} {suburb}", cancellationToken);

        return new SearchAreasResponse(matches
            .Select(match => new AreaMatchDto(
                ShipperAddress.ComposeAreaName(postcode, suburb, match.City), match.Country, match.Latitude, match.Longitude))
            .DistinctBy(match => (match.AreaName, match.Country))
            .ToList());
    }
}
