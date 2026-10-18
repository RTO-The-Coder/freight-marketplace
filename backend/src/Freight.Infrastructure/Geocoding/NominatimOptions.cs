namespace Freight.Infrastructure.Geocoding;

/// <summary>
/// Configuration for <see cref="NominatimGeocodingService"/>, bound from the "Geocoding"
/// section. Nominatim's usage policy requires an identifying User-Agent: the application name
/// and the project's public page, so it works without any local settings. A
/// <see cref="ContactEmail"/> is optional and, when set, lives only in local config
/// (appsettings.Development.json, gitignored).
/// </summary>
public sealed class NominatimOptions
{
    public const string SectionName = "Geocoding";

    /// <summary>Root of the Nominatim HTTP API.</summary>
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>The application name sent in the User-Agent header.</summary>
    public string UserAgent { get; set; } = "freight-marketplace/1.0";

    /// <summary>The project's public page, sent in the User-Agent header so Nominatim can see who runs the app.</summary>
    public string ProjectUrl { get; set; } = "https://github.com/RTO-The-Coder/freight-marketplace";

    /// <summary>Optional contact address added to the User-Agent header.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>How long to wait for a single call before treating Nominatim as unavailable.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Minimum gap between consecutive calls, enforced process-wide by
    /// <see cref="ThrottlingGeocodingService"/>. The usage policy allows at most 1 request/second.
    /// </summary>
    public int MinRequestIntervalMilliseconds { get; set; } = 1100;

    /// <summary>
    /// The full User-Agent header value, e.g.
    /// <c>freight-marketplace/1.0 (+https://github.com/RTO-The-Coder/freight-marketplace)</c>.
    /// </summary>
    public string UserAgentHeader => string.IsNullOrWhiteSpace(ContactEmail)
        ? $"{UserAgent} (+{ProjectUrl})"
        : $"{UserAgent} (+{ProjectUrl}; {ContactEmail})";
}
