namespace Freight.Domain.Fleet;

/// <summary>
/// The Firebase Installation ID (FID) of one TruckingCompany's dispatcher device - what FCM
/// push notifications are addressed to. Single active device per company for this phase
/// (ADR 0007/0003): registering again for a company replaces its old FID.
/// <see cref="Fid"/> is stored already encrypted (see
/// Freight.Infrastructure.Security.DeviceTokenEncryptor) - this entity has no opinion on
/// that, it just stores whatever string it is given.
/// </summary>
public sealed class DeviceToken
{
    public Guid Id { get; private set; }
    public Guid TruckingCompanyId { get; private set; }
    public string Fid { get; private set; } = null!;
    public DateTime RegisteredAt { get; private set; }

    private DeviceToken()
    {
    }

    private DeviceToken(Guid id, Guid truckingCompanyId, string fid, DateTime registeredAt)
    {
        Id = id;
        TruckingCompanyId = truckingCompanyId;
        Fid = fid;
        RegisteredAt = registeredAt;
    }

    public static DeviceToken Create(Guid id, Guid truckingCompanyId, string fid, DateTime registeredAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Device token id cannot be empty.", nameof(id));
        }

        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        if (string.IsNullOrWhiteSpace(fid))
        {
            throw new ArgumentException("Fid is required.", nameof(fid));
        }

        return new DeviceToken(id, truckingCompanyId, fid, registeredAt);
    }

    /// <summary>Replaces the stored (encrypted) FID when a company re-registers.</summary>
    public void ReplaceFid(string fid, DateTime registeredAt)
    {
        if (string.IsNullOrWhiteSpace(fid))
        {
            throw new ArgumentException("Fid is required.", nameof(fid));
        }

        Fid = fid;
        RegisteredAt = registeredAt;
    }
}
