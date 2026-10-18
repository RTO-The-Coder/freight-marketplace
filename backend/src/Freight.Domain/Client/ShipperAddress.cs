using Freight.Domain.ValueObjects;

namespace Freight.Domain.Client;

/// <summary>
/// One entry of a <see cref="Shipper"/>'s address book, owned by the shipper (no repository of
/// its own) and created only through <see cref="Shipper.AddAddress"/>. A generic area, never a
/// street address (ADR 0013): the shipper's own <see cref="Label"/> ("Main warehouse"), the
/// <see cref="AreaName"/> found by postcode and suburb ("1100 Favoriten, Vienna") and that
/// area's <see cref="Location"/>. Any entry can be a pickup or a delivery; booking copies its
/// name and coordinates onto the shipment, so the shipment never links back here. Add-only:
/// never edited or deleted.
/// </summary>
public sealed class ShipperAddress
{
    public const int MaxLabelLength = 100;
    public const int MaxAreaNameLength = 300;

    public Guid Id { get; private set; }
    public string Label { get; private set; } = null!;
    public string AreaName { get; private set; } = null!;
    public GeoLocation Location { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    // EF Core materializer only - Shipper.AddAddress is the sole construction path.
    private ShipperAddress()
    {
    }

    internal static ShipperAddress Create(string label, string areaName, GeoLocation location, DateTime createdAt)
    {
        var trimmedLabel = label?.Trim() ?? string.Empty;
        if (trimmedLabel.Length == 0 || trimmedLabel.Length > MaxLabelLength)
        {
            throw new ArgumentException($"An address name of 1-{MaxLabelLength} characters is required.", nameof(label));
        }

        var trimmedAreaName = areaName?.Trim() ?? string.Empty;
        if (trimmedAreaName.Length == 0 || trimmedAreaName.Length > MaxAreaNameLength)
        {
            throw new ArgumentException($"An area of 1-{MaxAreaNameLength} characters is required.", nameof(areaName));
        }

        ArgumentNullException.ThrowIfNull(location);

        return new ShipperAddress
        {
            Id = Guid.NewGuid(),
            Label = trimmedLabel,
            AreaName = trimmedAreaName,
            Location = location,
            CreatedAt = createdAt,
        };
    }

    /// <summary>The place name a shipment booked from this address carries, e.g. "Main warehouse — 1100 Favoriten, Vienna".</summary>
    public string PlaceName => $"{Label} — {AreaName}";

    /// <summary>
    /// The generic area text every saved address is named by: <c>"&lt;postcode&gt; &lt;suburb&gt;, &lt;city&gt;"</c>,
    /// e.g. "1100 Favoriten, Vienna". The suburb is the one the shipper typed (the geocoder's
    /// district names are often administrative or a neighbouring district); postcode and city
    /// come from the found area. Missing parts are left out; a suburb equal to its city is shown once.
    /// </summary>
    public static string ComposeAreaName(string? postcode, string? suburb, string? city)
    {
        static string Clean(string? part) => string.Join(' ', (part ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        var cleanPostcode = Clean(postcode);
        var cleanSuburb = Clean(suburb);
        var cleanCity = Clean(city);
        if (string.Equals(cleanSuburb, cleanCity, StringComparison.OrdinalIgnoreCase))
        {
            cleanSuburb = string.Empty;
        }

        var local = string.Join(' ', new[] { cleanPostcode, cleanSuburb }.Where(part => part.Length > 0));
        return string.Join(", ", new[] { local, cleanCity }.Where(part => part.Length > 0));
    }
}
