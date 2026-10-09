namespace Freight.Domain.Notifications.Abstractions;

/// <summary>
/// Sends shipment notifications: an open shipment goes to every TruckingCompany, unconditionally
/// and with no eligibility filtering (ADR 0007); a direct shipment goes only to the company it
/// was booked to. The real implementation is FCM push (ADR 0003); a log-based implementation is
/// the fallback when push isn't configured, so booking a Shipment never fails just because
/// notification delivery couldn't happen.
/// </summary>
public interface INotificationSender
{
    Task NotifyAllCompaniesAsync(ShipmentNotificationSummary summary, CancellationToken cancellationToken = default);

    Task NotifyCompanyAsync(Guid truckingCompanyId, ShipmentNotificationSummary summary, CancellationToken cancellationToken = default);
}
