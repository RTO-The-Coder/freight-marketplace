using System.Security.Cryptography;
using Freight.Domain.Fleet.Abstractions;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Security;

/// <summary>
/// Real (AES-GCM) implementation of <see cref="IDeviceTokenEncryptor"/>. The key comes
/// from <see cref="DeviceTokenEncryptionOptions"/>, never from source.
/// </summary>
public sealed class DeviceTokenEncryptor : IDeviceTokenEncryptor
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public DeviceTokenEncryptor(IOptions<DeviceTokenEncryptionOptions> options)
    {
        var keyBase64 = options.Value.Key;
        if (string.IsNullOrWhiteSpace(keyBase64))
        {
            throw new InvalidOperationException(
                $"'{DeviceTokenEncryptionOptions.SectionName}:Key' is not configured.");
        }

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException(
                $"'{DeviceTokenEncryptionOptions.SectionName}:Key' must decode to a 128/192/256-bit AES key.");
        }
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // nonce || tag || ciphertext, base64-encoded as one string for the DB column.
        var payload = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSizeBytes + TagSizeBytes, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string encoded)
    {
        var payload = Convert.FromBase64String(encoded);
        var nonce = payload[..NonceSizeBytes];
        var tag = payload[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        var ciphertext = payload[(NonceSizeBytes + TagSizeBytes)..];

        var plaintextBytes = new byte[ciphertext.Length];
        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
