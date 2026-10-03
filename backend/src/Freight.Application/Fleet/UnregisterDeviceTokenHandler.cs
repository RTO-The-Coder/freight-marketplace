using Freight.Domain.Common;
using Freight.Domain.Fleet.Abstractions;

namespace Freight.Application.Fleet;

public sealed record UnregisterDeviceTokenRequest(Guid CompanyId, string Fid);

/// <summary>
/// Removes a TruckingCompany's registered device - called by the mobile app when the user
/// has switched notifications off, so the backend stops sending to it. Only removes the
/// registration if it is still this device's FID: another phone may have registered for the
/// company since (one device per company, registering replaces), and that one must keep
/// receiving. Nothing registered, or a different device's FID, is a no-op, not an error.
/// </summary>
public sealed class UnregisterDeviceTokenHandler(IUnitOfWork unitOfWork, IDeviceTokenEncryptor encryptor)
{
    public async Task UnregisterAsync(UnregisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Fid))
        {
            throw new ArgumentException("Fid is required.", nameof(request));
        }

        var existing = await unitOfWork.DeviceTokens.GetByTruckingCompanyIdAsync(request.CompanyId, cancellationToken);
        if (existing is null || encryptor.Decrypt(existing.Fid) != request.Fid)
        {
            return;
        }

        unitOfWork.DeviceTokens.Remove(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
