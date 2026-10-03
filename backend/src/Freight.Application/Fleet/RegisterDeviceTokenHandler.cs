using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;

namespace Freight.Application.Fleet;

public sealed record RegisterDeviceTokenRequest(Guid CompanyId, string Fid);

/// <summary>
/// Registers (or replaces) the Firebase Installation ID (FID) of a TruckingCompany's
/// dispatcher device (ADR 0007/0003). Called by the mobile app on launch and whenever its
/// FID changes; the FID is encrypted before it touches the database.
/// </summary>
public sealed class RegisterDeviceTokenHandler(
    IUnitOfWork unitOfWork, IDeviceTokenEncryptor encryptor, TimeProvider timeProvider)
{
    public async Task RegisterAsync(RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Fid))
        {
            throw new ArgumentException("Fid is required.", nameof(request));
        }

        _ = await unitOfWork.TruckingCompanies.GetByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{request.CompanyId}' was not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var encryptedFid = encryptor.Encrypt(request.Fid);

        var existing = await unitOfWork.DeviceTokens.GetByTruckingCompanyIdAsync(request.CompanyId, cancellationToken);
        if (existing is null)
        {
            unitOfWork.DeviceTokens.Add(DeviceToken.Create(Guid.NewGuid(), request.CompanyId, encryptedFid, now));
        }
        else
        {
            existing.ReplaceFid(encryptedFid, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
