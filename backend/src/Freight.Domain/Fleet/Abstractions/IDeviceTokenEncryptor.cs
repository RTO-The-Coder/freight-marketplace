namespace Freight.Domain.Fleet.Abstractions;

/// <summary>
/// Encrypts/decrypts device tokens before they touch the database - the token itself is
/// low-sensitivity (it can only be used to receive a push, not to authenticate as the
/// company or call any API), but this keeps stored values unreadable to anyone with DB
/// access but not the key. See Freight.Infrastructure.Security.DeviceTokenEncryptor for
/// the real (AES-GCM) implementation.
/// </summary>
public interface IDeviceTokenEncryptor
{
    string Encrypt(string plaintext);

    string Decrypt(string encoded);
}
