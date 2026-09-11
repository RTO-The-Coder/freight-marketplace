using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class RescheduleTripHandlerTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);

    private static (Trip Trip, Truck Truck) OpenTripWithShipment()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        var trip = Trip.Open(truck.Id, Guid.NewGuid(), StartedAt);
        trip.AssignShipment(
            Guid.NewGuid(), Capacity.Create(100, 1),
            GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582), GeoLocation.Create(50.11, 8.68),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new Freight.Domain.Fleet.ValueObjects.LegPlan(
                new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6), null,
                new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6), null,
                new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6)));
        return (trip, truck);
    }

    private static Mock<IUnitOfWork> SetUp(Trip? trip, Truck? truck)
    {
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(trip);
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task RescheduleTripAsync_NoCurrentProgress_TreatsAsNotStartedDriving()
    {
        var (trip, truck) = OpenTripWithShipment();
        var unitOfWork = SetUp(trip, truck);
        var newStart = StartedAt.AddHours(2);

        var response = await RescheduleAsync(unitOfWork, trip.Id, newStart);

        Assert.Equal(newStart, trip.StartedAt);
        Assert.Equal(newStart, response.StartedAt);
    }

    [Fact]
    public async Task RescheduleTripAsync_CurrentProgressZeroTicks_TreatsAsNotStartedDriving()
    {
        var (trip, truck) = OpenTripWithShipment();
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        // CurrentProgress now exists but CurrentDrivingTimeTick is still 0 - not yet moved.
        var unitOfWork = SetUp(trip, truck);
        var newStart = StartedAt.AddHours(2);

        var response = await RescheduleAsync(unitOfWork, trip.Id, newStart);

        Assert.Equal(newStart, trip.StartedAt);
    }

    [Fact]
    public async Task RescheduleTripAsync_CurrentProgressPositiveTicks_TreatedAsStartedDriving_DomainRejectionPropagates()
    {
        var (trip, truck) = OpenTripWithShipment();
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        truck.CurrentProgress!.AdvanceByTicks(1);
        var unitOfWork = SetUp(trip, truck);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RescheduleAsync(unitOfWork, trip.Id, StartedAt.AddHours(2)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RescheduleTripAsync_UnknownTripId_Throws_NeverSaves()
    {
        var unitOfWork = SetUp(null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RescheduleAsync(unitOfWork, Guid.NewGuid(), StartedAt.AddHours(2)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RescheduleTripAsync_TripsTruckNotFound_Throws_NeverSaves()
    {
        var (trip, _) = OpenTripWithShipment();
        var unitOfWork = SetUp(trip, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RescheduleAsync(unitOfWork, trip.Id, StartedAt.AddHours(2)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task<RescheduleTripResponse> RescheduleAsync(Mock<IUnitOfWork> unitOfWork, Guid tripId, DateTime newStart)
    {
        var handler = new RescheduleTripHandler(unitOfWork.Object);
        return await handler.RescheduleTripAsync(new RescheduleTripRequest(tripId, newStart));
    }
}
