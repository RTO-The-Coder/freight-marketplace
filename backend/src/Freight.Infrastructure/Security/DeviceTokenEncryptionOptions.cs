namespace Freight.Infrastructure.Security;

/// <summary>
/// Configuration for <see cref="DeviceTokenEncryptor"/>, bound from the
/// "DeviceTokenEncryption" section. The key never lives in source - it is provided via
/// local config (appsettings.Development.json, gitignored) or, in a real deployment, an
/// environment variable/secret store.
/// </summary>
public sealed class DeviceTokenEncryptionOptions
{
    public const string SectionName = "DeviceTokenEncryption";

    /// <summary>Base64-encoded 256-bit AES key.</summary>
    public string Key { get; set; } = string.Empty;
}
