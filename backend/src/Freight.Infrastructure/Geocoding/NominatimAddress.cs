using System.Text.Json.Serialization;

namespace Freight.Infrastructure.Geocoding;

/// <summary>
/// The parts of a Nominatim <c>address</c> object (<c>addressdetails=1</c>) this app reads:
/// postcode, city and country. Street, house number and suburb are deliberately not read - a
/// saved address is a generic area named by the suburb the shipper typed (ADR 0013).
/// </summary>
public sealed record NominatimAddress(
    [property: JsonPropertyName("postcode")] string? Postcode = null,
    [property: JsonPropertyName("city")] string? City = null,
    [property: JsonPropertyName("town")] string? Town = null,
    [property: JsonPropertyName("municipality")] string? Municipality = null,
    [property: JsonPropertyName("village")] string? Village = null,
    [property: JsonPropertyName("county")] string? County = null,
    [property: JsonPropertyName("country")] string? Country = null)
{
    /// <summary>The settlement the area belongs to: city, else town, municipality, village or county.</summary>
    public string CityName =>
        new[] { City, Town, Municipality, Village, County }
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim() ?? string.Empty;
}
