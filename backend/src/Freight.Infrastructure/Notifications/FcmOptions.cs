namespace Freight.Infrastructure.Notifications;

/// <summary>
/// Configuration for <see cref="FcmNotificationSender"/>, bound from the "Fcm" section.
/// The service-account JSON itself is a secret and is never committed - only this path,
/// set locally in appsettings.Development.json (gitignored), points at it.
/// </summary>
public sealed class FcmOptions
{
    public const string SectionName = "Fcm";

    /// <summary>
    /// Absolute path to the Firebase service-account JSON key file. When empty or the
    /// file doesn't exist, <see cref="LogNotificationSender"/> is used instead so the app
    /// still runs without Firebase configured (ADR 0003).
    /// </summary>
    public string ServiceAccountPath { get; set; } = string.Empty;
}
