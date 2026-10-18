namespace Freight.Domain.Geocoding.Abstractions;

/// <summary>
/// Finds the areas matching a generic place description - a postcode and suburb, never a
/// street address (ADR 0013). Used only to fill a shipper's address book: the found
/// coordinates are copied onto the saved address, and from there onto a booked shipment, so
/// routing never calls this. Domain/Application depend on the interface, Infrastructure
/// implements it against Nominatim (OpenStreetMap). A failed call throws
/// <see cref="GeocodingUnavailableException"/>; no matches is an empty list, not an error.
/// </summary>
public interface IGeocodingService
{
    /// <summary>Up to five areas matching <paramref name="query"/>, best match first.</summary>
    /// <exception cref="GeocodingUnavailableException">The provider was unreachable, timed out or errored.</exception>
    Task<IReadOnlyList<GeocodingMatch>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// One found area: where it is, and the postcode, city and country the provider places it in
/// (empty when unknown). The suburb is deliberately not taken from the provider - its district
/// names are often administrative or a neighbouring district, so the area is named by the
/// suburb the shipper typed (<see cref="Client.ShipperAddress.ComposeAreaName"/>).
/// </summary>
public sealed record GeocodingMatch(string Postcode, string City, string Country, double Latitude, double Longitude);
