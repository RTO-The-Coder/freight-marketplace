using Freight.Domain.Fleet.Abstractions;

namespace Freight.Infrastructure.Security;

/// <summary>
/// Stand-in for <see cref="DeviceTokenEncryptor"/> when no key is configured (CI, tests, a
/// machine without the local config). The rest of the API keeps working; only push
/// registration is disabled, and it fails with a clear message when actually used - the
/// same "runs without the secret" approach as the log-based notification sender.
/// </summary>
public sealed class UnconfiguredDeviceTokenEncryptor : IDeviceTokenEncryptor
{
    public const string Message =
        $"{DeviceTokenEncryptionOptions.SectionName}:Key is not configured, so push registration is disabled.";

    public string Encrypt(string plaintext) => throw new InvalidOperationException(Message);

    public string Decrypt(string encoded) => throw new InvalidOperationException(Message);
}
