using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freight.Domain.Geocoding.Abstractions;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Geocoding;

/// <summary>
/// <see cref="IGeocodingService"/> backed by Nominatim (OpenStreetMap). One call per search:
/// <c>GET {base}/search?q=...&amp;format=jsonv2&amp;addressdetails=1&amp;accept-language=en&amp;limit=5</c>,
/// with the identifying User-Agent the usage policy requires. Each result is read as its
/// coordinates plus postcode, city and country (<see cref="NominatimAddress"/>). Any transport
/// failure, a non-success status or an unreadable response becomes
/// <see cref="GeocodingUnavailableException"/>; an empty result is an empty list.
///
/// <see cref="ReverseAsync"/> (coordinates to area) is used only by the seeder's fixture
/// builder to name the fixed demo places once, offline from every seed run.
/// </summary>
public sealed class NominatimGeocodingService(HttpClient httpClient, IOptions<NominatimOptions> options) : IGeocodingService
{
    private const int MaxMatches = 5;

    private readonly NominatimOptions _options = options.Value;

    public async Task<IReadOnlyList<GeocodingMatch>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var path = $"search?q={Uri.EscapeDataString(query.Trim())}&format=jsonv2&addressdetails=1&accept-language=en&limit={MaxMatches}";
        var places = await GetAsync<List<NominatimPlace>>(path, $"search '{query.Trim()}'", cancellationToken) ?? [];

        return places
            .Select(ToMatch)
            .OfType<GeocodingMatch>()
            .ToList();
    }

    /// <summary>The area around a coordinate, or null when Nominatim knows none there.</summary>
    /// <exception cref="GeocodingUnavailableException">Unreachable, timed out or errored.</exception>
    public async Task<GeocodingMatch?> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        static string Coord(double value) => value.ToString("0.0#####", CultureInfo.InvariantCulture);

        var path = $"reverse?lat={Coord(latitude)}&lon={Coord(longitude)}&format=jsonv2&addressdetails=1&accept-language=en&zoom=16";
        var place = await GetAsync<NominatimPlace>(path, $"reverse ({Coord(latitude)},{Coord(longitude)})", cancellationToken);

        return place is null ? null : ToMatch(place);
    }

    private async Task<T?> GetAsync<T>(string path, string label, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.UserAgent.ParseAdd(_options.UserAgentHeader);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new GeocodingUnavailableException(
                    $"Nominatim returned {(int)response.StatusCode} {response.ReasonPhrase} for {label}.");
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            throw new GeocodingUnavailableException($"Nominatim was unreachable for {label}: {ex.Message}", ex);
        }
    }

    private static GeocodingMatch? ToMatch(NominatimPlace place)
    {
        if (!double.TryParse(place.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(place.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        return new GeocodingMatch(
            place.Address?.Postcode?.Trim() ?? string.Empty,
            place.Address?.CityName ?? string.Empty,
            place.Address?.Country?.Trim() ?? string.Empty,
            latitude,
            longitude);
    }

    // Nominatim sends lat/lon as strings; reverse with no result sends an "error" object instead.
    private sealed record NominatimPlace(
        [property: JsonPropertyName("lat")] string? Lat,
        [property: JsonPropertyName("lon")] string? Lon,
        [property: JsonPropertyName("address")] NominatimAddress? Address);
}
