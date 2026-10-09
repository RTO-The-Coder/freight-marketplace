using Freight.Domain.Notifications;
using Freight.Domain.Notifications.Abstractions;
using Microsoft.Extensions.Logging;

namespace Freight.Infrastructure.Notifications;

/// <summary>
/// No-op default <see cref="INotificationSender"/> - logs instead of sending a real push.
/// Used when Fcm:ServiceAccountPath isn't configured, so booking a Shipment never fails
/// just because Firebase isn't set up (ADR 0003's "clone and run without external keys").
/// </summary>
public sealed class LogNotificationSender(ILogger<LogNotificationSender> logger) : INotificationSender
{
    public Task NotifyAllCompaniesAsync(ShipmentNotificationSummary summary, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Shipment {ShipmentId} booked (pickup {PickupLatitude},{PickupLongitude}, {TruckType}, window {Earliest}-{Latest}) - " +
            "no push sent, Fcm:ServiceAccountPath is not configured.",
            summary.ShipmentId,
            summary.PickupLocation.Latitude,
            summary.PickupLocation.Longitude,
            summary.RequiredTruckType,
            summary.PickupWindow.Earliest,
            summary.PickupWindow.Latest);

        return Task.CompletedTask;
    }

    public Task NotifyCompanyAsync(
        Guid truckingCompanyId, ShipmentNotificationSummary summary, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Shipment {ShipmentId} booked directly to company {TruckingCompanyId} (pickup {PickupLatitude},{PickupLongitude}, " +
            "{TruckType}, window {Earliest}-{Latest}) - no push sent, Fcm:ServiceAccountPath is not configured.",
            summary.ShipmentId,
            truckingCompanyId,
            summary.PickupLocation.Latitude,
            summary.PickupLocation.Longitude,
            summary.RequiredTruckType,
            summary.PickupWindow.Earliest,
            summary.PickupWindow.Latest);

        return Task.CompletedTask;
    }
}
