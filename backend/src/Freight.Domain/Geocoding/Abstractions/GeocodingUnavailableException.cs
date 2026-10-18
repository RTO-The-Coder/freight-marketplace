namespace Freight.Domain.Geocoding.Abstractions;

/// <summary>
/// The geocoding provider could not answer - unreachable, timed out or errored. Not the
/// caller's fault and retryable, so it surfaces as HTTP 503.
/// </summary>
public sealed class GeocodingUnavailableException : Exception
{
    public GeocodingUnavailableException(string message)
        : base(message)
    {
    }

    public GeocodingUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
