namespace Freight.Domain.Client.Enums;

/// <summary>
/// Stored status only. An offer past its own limit or its shipment's offer deadline stays
/// Pending in storage but is no longer waiting - see <see cref="ShipmentOffer.IsWaiting"/>.
/// </summary>
public enum ShipmentOfferStatus
{
    Pending,
    Accepted,
    Rejected
}
