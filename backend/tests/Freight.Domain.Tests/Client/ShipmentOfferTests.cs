using Freight.Domain.Client;
using Freight.Domain.Client.Enums;

namespace Freight.Domain.Tests.Client;

public class ShipmentOfferTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime OfferDeadline = CreatedAt.AddHours(2);

    private static ShipmentOffer NewOffer(DateTime? limitAt = null, decimal price = 850m) => ShipmentOffer.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 2, 12.5, price, limitAt, CreatedAt);

    [Fact]
    public void Create_SetsPropertiesAndPendingStatus()
    {
        var shipmentId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var truckId = Guid.NewGuid();
        var limitAt = CreatedAt.AddMinutes(10);

        var offer = ShipmentOffer.Create(shipmentId, companyId, truckId, 1, 3, 12.5, 850m, limitAt, CreatedAt);

        Assert.NotEqual(Guid.Empty, offer.Id);
        Assert.Equal(shipmentId, offer.ShipmentId);
        Assert.Equal(companyId, offer.TruckingCompanyId);
        Assert.Equal(truckId, offer.TruckId);
        Assert.Equal(1, offer.PickupInsertIndex);
        Assert.Equal(3, offer.DeliveryInsertIndex);
        Assert.Equal(12.5, offer.AddedDistanceKm);
        Assert.Equal(850m, offer.PriceEur);
        Assert.Equal(limitAt, offer.LimitAt);
        Assert.Equal(CreatedAt, offer.CreatedAt);
        Assert.Equal(ShipmentOfferStatus.Pending, offer.Status);
    }

    [Fact]
    public void Create_EmptyIds_Throw()
    {
        Assert.Throws<ArgumentException>(() =>
            ShipmentOffer.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 0, 1, 0, 1m, null, CreatedAt));
        Assert.Throws<ArgumentException>(() =>
            ShipmentOffer.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 0, 1, 0, 1m, null, CreatedAt));
        Assert.Throws<ArgumentException>(() =>
            ShipmentOffer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 0, 1, 0, 1m, null, CreatedAt));
    }

    [Fact]
    public void Create_NegativePositionsOrDistance_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ShipmentOffer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1, 1, 0, 1m, null, CreatedAt));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ShipmentOffer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, -1, 0, 1m, null, CreatedAt));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ShipmentOffer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, 1, -0.1, 1m, null, CreatedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_PriceNotAboveZero_Throws(int price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewOffer(price: price));
    }

    [Fact]
    public void Create_LimitNotInFuture_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewOffer(limitAt: CreatedAt));
        Assert.Throws<ArgumentException>(() => NewOffer(limitAt: CreatedAt.AddMinutes(-1)));
    }

    [Fact]
    public void IsWaiting_NoLimit_UntilShipmentDeadline()
    {
        var offer = NewOffer();

        Assert.True(offer.IsWaiting(OfferDeadline.AddTicks(-1), OfferDeadline));
        Assert.False(offer.IsWaiting(OfferDeadline, OfferDeadline));
    }

    [Fact]
    public void IsWaiting_OwnLimit_UntilLimit()
    {
        var limitAt = CreatedAt.AddMinutes(10);
        var offer = NewOffer(limitAt);

        Assert.True(offer.IsWaiting(limitAt.AddTicks(-1), OfferDeadline));
        Assert.False(offer.IsWaiting(limitAt, OfferDeadline));
    }

    [Fact]
    public void IsWaiting_RejectedOffer_False()
    {
        var offer = NewOffer();
        offer.Reject();

        Assert.False(offer.IsWaiting(CreatedAt, OfferDeadline));
    }

    [Fact]
    public void Accept_WhileWaiting_SetsAccepted()
    {
        var offer = NewOffer();

        offer.Accept(CreatedAt.AddMinutes(5), OfferDeadline);

        Assert.Equal(ShipmentOfferStatus.Accepted, offer.Status);
    }

    [Fact]
    public void Accept_AfterOwnLimit_Throws()
    {
        var offer = NewOffer(CreatedAt.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() => offer.Accept(CreatedAt.AddMinutes(10), OfferDeadline));
        Assert.Equal(ShipmentOfferStatus.Pending, offer.Status);
    }

    [Fact]
    public void Accept_AfterShipmentDeadline_Throws()
    {
        var offer = NewOffer();

        Assert.Throws<InvalidOperationException>(() => offer.Accept(OfferDeadline, OfferDeadline));
    }

    [Fact]
    public void Accept_Rejected_Throws()
    {
        var offer = NewOffer();
        offer.Reject();

        Assert.Throws<InvalidOperationException>(() => offer.Accept(CreatedAt, OfferDeadline));
    }

    [Fact]
    public void Reject_WhilePending_SetsRejected()
    {
        var offer = NewOffer();

        offer.Reject();

        Assert.Equal(ShipmentOfferStatus.Rejected, offer.Status);
    }

    [Fact]
    public void Reject_Accepted_Throws()
    {
        var offer = NewOffer();
        offer.Accept(CreatedAt, OfferDeadline);

        Assert.Throws<InvalidOperationException>(() => offer.Reject());
    }
}
