using System.Security.Cryptography;
using Freight.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Tests;

public class DeviceTokenEncryptorTests
{
    private static DeviceTokenEncryptor NewEncryptor(string? keyBase64 = null) =>
        new(Options.Create(new DeviceTokenEncryptionOptions
        {
            Key = keyBase64 ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }));

    [Fact]
    public void Decrypt_OfEncrypt_ReturnsOriginal()
    {
        var encryptor = NewEncryptor();
        const string fid = "cXyZ-firebase-installation-id_123";

        var encrypted = encryptor.Encrypt(fid);

        Assert.NotEqual(fid, encrypted);
        Assert.DoesNotContain(fid, encrypted);
        Assert.Equal(fid, encryptor.Decrypt(encrypted));
    }

    [Fact]
    public void Encrypt_SameInputTwice_GivesDifferentCiphertext()
    {
        var encryptor = NewEncryptor();

        Assert.NotEqual(encryptor.Encrypt("same-fid"), encryptor.Encrypt("same-fid"));
    }

    [Fact]
    public void Decrypt_WithDifferentKey_Throws()
    {
        var encrypted = NewEncryptor().Encrypt("fid");

        Assert.ThrowsAny<CryptographicException>(() => NewEncryptor().Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var encryptor = NewEncryptor();
        var payload = Convert.FromBase64String(encryptor.Encrypt("fid"));
        payload[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => encryptor.Decrypt(Convert.ToBase64String(payload)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_MissingKey_Throws(string key)
    {
        Assert.Throws<InvalidOperationException>(() => NewEncryptor(key));
    }

    [Fact]
    public void Constructor_KeyOfWrongLength_Throws()
    {
        var tenBytes = Convert.ToBase64String(RandomNumberGenerator.GetBytes(10));

        Assert.Throws<InvalidOperationException>(() => NewEncryptor(tenBytes));
    }
}
