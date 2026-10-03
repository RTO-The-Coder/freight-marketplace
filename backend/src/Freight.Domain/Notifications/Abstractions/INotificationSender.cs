namespace Freight.Domain.Notifications.Abstractions;

/// <summary>
/// Sends the "a Shipment was booked" notification to every TruckingCompany, unconditionally
/// and with no eligibility filtering (ADR 0007). The real implementation is FCM push
/// (ADR 0003); a log-based implementation is the fallback when push isn't configured, so
/// booking a Shipment never fails just because notification delivery couldn't happen.
/// </summary>
public interface INotificationSender
{
    Task NotifyAllCompaniesAsync(ShipmentNotificationSummary summary, CancellationToken cancellationToken = default);
}
