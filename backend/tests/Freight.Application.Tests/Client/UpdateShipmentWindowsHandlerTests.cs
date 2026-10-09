using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Notifications;
using Freight.Domain.Notifications.Abstractions;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Client;

public sealed class UpdateShipmentWindowsHandlerTests
{
    private static readonly DateTime BookedAt = new(2026, 1, 1, 6, 0, 0);

    private static Shipment SomeShipment(Guid? directCompanyId = null) => Shipment.Book(
        Guid.NewGuid(), Guid.NewGuid(),
        GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
        Capacity.Create(100, 5), TruckType.Refrigerated,
        TimeWindow.Create(BookedAt.AddHours(2), BookedAt.AddHours(4)),
        TimeWindow.Create(BookedAt.AddHours(8), BookedAt.AddHours(10)),
        BookedAt,
        directCompanyId);

    private static TimeWindow NewPickup() => TimeWindow.Create(BookedAt.AddHours(5), BookedAt.AddHours(6));
    private static TimeWindow NewDelivery() => TimeWindow.Create(BookedAt.AddHours(9), BookedAt.AddHours(11));

    private sealed record Setup(
        UpdateShipmentWindowsHandler Handler, Mock<IUnitOfWork> UnitOfWork, Mock<INotificationSender> Sender);

    private static Setup Arrange(Shipment? shipment, IReadOnlyList<ShipmentOffer>? offers = null, DateTime? now = null)
    {
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        var offerRepo = new Mock<IShipmentOfferRepository>();
        offerRepo.Setup(r => r.GetByShipmentIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(offers ?? []);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);
        unitOfWork.SetupGet(u => u.ShipmentOffers).Returns(offerRepo.Object);
        FakeSimulationClock.SetUp(unitOfWork, now ?? BookedAt.AddHours(3));
        var sender = new Mock<INotificationSender>();

        return new Setup(
            new UpdateShipmentWindowsHandler(unitOfWork.Object, new FakeTimeProvider(DateTimeOffset.UtcNow), sender.Object),
            unitOfWork,
            sender);
    }

    [Fact]
    public async Task HandleAsync_OpenShipment_UpdatesBothWindowsRestartsDeadlineAndSavesOnce()
    {
        var shipment = SomeShipment();
        var clockTime = BookedAt.AddHours(3);
        var setup = Arrange(shipment, now: clockTime);
        var pickup = NewPickup();
        var delivery = NewDelivery();

        await setup.Handler.HandleAsync(new UpdateShipmentWindowsRequest(shipment.Id, pickup, delivery));

        Assert.Same(pickup, shipment.PickupWindow);
        Assert.Same(delivery, shipment.DeliveryWindow);
        Assert.Equal(clockTime.AddHours(2), shipment.OfferDeadline);
        setup.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RejectsPendingOffers_LeavesOthersAlone()
    {
        var shipment = SomeShipment();
        var pending = ShipmentOffer.Create(shipment.Id, Guid.NewGuid(), Guid.NewGuid(), 0, 1, 5, 500m, null, BookedAt);
        var alreadyRejected = ShipmentOffer.Create(shipment.Id, Guid.NewGuid(), Guid.NewGuid(), 0, 1, 5, 600m, null, BookedAt);
        alreadyRejected.Reject();
        var setup = Arrange(shipment, [pending, alreadyRejected]);

        await setup.Handler.HandleAsync(new UpdateShipmentWindowsRequest(shipment.Id, NewPickup(), NewDelivery()));

        Assert.Equal(ShipmentOfferStatus.Rejected, pending.Status);
        Assert.Equal(ShipmentOfferStatus.Rejected, alreadyRejected.Status);
    }

    [Fact]
    public async Task HandleAsync_OpenShipment_PushesToAllCompanies()
    {
        var shipment = SomeShipment();
        var setup = Arrange(shipment);

        await setup.Handler.HandleAsync(new UpdateShipmentWindowsRequest(shipment.Id, NewPickup(), NewDelivery()));

        setup.Sender.Verify(s => s.NotifyAllCompaniesAsync(
            It.Is<ShipmentNotificationSummary>(summary => summary.ShipmentId == shipment.Id && !summary.IsDirect),
            It.IsAny<CancellationToken>()), Times.Once);
        setup.Sender.Verify(s => s.NotifyCompanyAsync(
            It.IsAny<Guid>(), It.IsAny<ShipmentNotificationSummary>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DirectShipment_PushesOnlyToItsCompany()
    {
        var companyId = Guid.NewGuid();
        var shipment = SomeShipment(companyId);
        var setup = Arrange(shipment);

        await setup.Handler.HandleAsync(new UpdateShipmentWindowsRequest(shipment.Id, NewPickup(), NewDelivery()));

        setup.Sender.Verify(s => s.NotifyCompanyAsync(
            companyId,
            It.Is<ShipmentNotificationSummary>(summary => summary.ShipmentId == shipment.Id && summary.IsDirect),
            It.IsAny<CancellationToken>()), Times.Once);
        setup.Sender.Verify(s => s.NotifyAllCompaniesAsync(
            It.IsAny<ShipmentNotificationSummary>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UnknownShipmentId_Throws_NeverSavesOrPushes()
    {
        var setup = Arrange(shipment: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Handler.HandleAsync(new UpdateShipmentWindowsRequest(Guid.NewGuid(), NewPickup(), NewDelivery())));

        setup.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        setup.Sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_AfterOfferAccepted_DomainRejectionPropagates_NeverSavesOrPushes()
    {
        var shipment = SomeShipment();
        shipment.AcceptOffer(Guid.NewGuid(), BookedAt.AddMinutes(10));
        var setup = Arrange(shipment);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Handler.HandleAsync(new UpdateShipmentWindowsRequest(shipment.Id, NewPickup(), NewDelivery())));

        setup.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        setup.Sender.VerifyNoOtherCalls();
    }
}
