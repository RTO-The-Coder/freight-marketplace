using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Notifications;
using Freight.Domain.Notifications.Abstractions;
using Microsoft.Extensions.Logging;

namespace Freight.Infrastructure.Notifications;

/// <summary>
/// Real FCM push implementation of <see cref="INotificationSender"/> (ADR 0003). Sends a
/// lightweight summary addressed by each device's Firebase Installation ID (FID): an open
/// shipment to every TruckingCompany with a registered device, unconditionally (ADR 0007), a
/// direct shipment only to its company. A failure for one device, or of FCM itself, is logged
/// and never fails the request that triggered it.
/// </summary>
public sealed class FcmNotificationSender(
    IUnitOfWork unitOfWork,
    IDeviceTokenEncryptor encryptor,
    FirebaseApp firebaseApp,
    ILogger<FcmNotificationSender> logger) : INotificationSender
{
    // FCM's limit on recipients per multicast request.
    private const int MaxRecipientsPerMulticast = 500;

    public async Task NotifyAllCompaniesAsync(ShipmentNotificationSummary summary, CancellationToken cancellationToken = default)
    {
        var deviceTokens = await unitOfWork.DeviceTokens.GetAllAsync(cancellationToken);
        await SendAsync(deviceTokens, summary, cancellationToken);
    }

    public async Task NotifyCompanyAsync(
        Guid truckingCompanyId, ShipmentNotificationSummary summary, CancellationToken cancellationToken = default)
    {
        var deviceToken = await unitOfWork.DeviceTokens.GetByTruckingCompanyIdAsync(truckingCompanyId, cancellationToken);
        if (deviceToken is null)
        {
            return;
        }

        await SendAsync([deviceToken], summary, cancellationToken);
    }

    private async Task SendAsync(
        IReadOnlyList<DeviceToken> deviceTokens, ShipmentNotificationSummary summary, CancellationToken cancellationToken)
    {
        var recipients = new List<(DeviceToken DeviceToken, string Fid)>();
        foreach (var deviceToken in deviceTokens)
        {
            try
            {
                recipients.Add((deviceToken, encryptor.Decrypt(deviceToken.Fid)));
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Could not decrypt the device FID of company {TruckingCompanyId}; skipping it.",
                    deviceToken.TruckingCompanyId);
            }
        }

        if (recipients.Count == 0)
        {
            return;
        }

        var messaging = FirebaseMessaging.GetMessaging(firebaseApp);

        foreach (var batch in recipients.Chunk(MaxRecipientsPerMulticast))
        {
            var message = new MulticastMessage
            {
                Fids = batch.Select(recipient => recipient.Fid).ToList(),
                Notification = new Notification
                {
                    Title = summary.IsDirect ? "New shipment assigned to you" : "New shipment available",
                    Body = $"{summary.RequiredTruckType} needed, pickup from " +
                           $"{summary.PickupLocation.Latitude:F4},{summary.PickupLocation.Longitude:F4} " +
                           $"by {summary.PickupWindow.Latest:g}.",
                },
                Data = new Dictionary<string, string>
                {
                    ["shipmentId"] = summary.ShipmentId.ToString(),
                    ["kind"] = summary.IsDirect ? "direct" : "open",
                },
            };

            BatchResponse response;
            try
            {
                response = await messaging.SendEachForMulticastAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send shipment {ShipmentId} notifications via FCM.", summary.ShipmentId);
                continue;
            }

            // Responses come back in the same order as the FIDs in the request.
            for (var i = 0; i < response.Responses.Count; i++)
            {
                if (response.Responses[i].IsSuccess)
                {
                    continue;
                }

                logger.LogWarning(
                    response.Responses[i].Exception,
                    "Failed to send shipment {ShipmentId} notification to company {TruckingCompanyId}.",
                    summary.ShipmentId,
                    batch[i].DeviceToken.TruckingCompanyId);
            }
        }
    }
}
