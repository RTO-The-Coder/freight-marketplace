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
/// Real FCM push implementation of <see cref="INotificationSender"/> (ADR 0003). Sends the
/// same lightweight summary to every TruckingCompany with a registered device, addressed by
/// its Firebase Installation ID (FID) - unconditionally, no eligibility filtering (ADR 0007).
/// A failure for one device, or of FCM itself, is logged and never fails the booking that
/// triggered it.
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
                    Title = "New shipment available",
                    Body = $"{summary.RequiredTruckType} needed, pickup from " +
                           $"{summary.PickupLocation.Latitude:F4},{summary.PickupLocation.Longitude:F4} " +
                           $"by {summary.PickupWindow.Latest:g}.",
                },
                Data = new Dictionary<string, string>
                {
                    ["shipmentId"] = summary.ShipmentId.ToString(),
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
