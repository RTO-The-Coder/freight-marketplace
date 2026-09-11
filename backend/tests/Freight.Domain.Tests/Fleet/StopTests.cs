using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests.Fleet;

public class StopTests
{
    private static GeoLocation SomeLocation() => GeoLocation.Create(52.5200, 13.4050);
    private static Capacity SomeLoad() => Capacity.Create(100, 1);

    private static Stop PickupStop(int waitTimeTick = 0) =>
        Stop.ForShipment(Guid.NewGuid(), SomeLoad(), StopKind.Pickup, SomeLocation(), sequence: 10, incomingLegDistanceKm: 50, incomingLegTimeTick: 6);

    private static Stop OfficeStop() =>
        Stop.ForOffice(Guid.NewGuid(), SomeLocation(), sequence: 10, incomingLegDistanceKm: 50, incomingLegTimeTick: 6);

    [Fact]
    public void ForShipment_ValidInput_SetsProperties()
    {
        var shipmentId = Guid.NewGuid();
        var load = SomeLoad();
        var location = SomeLocation();

        var stop = Stop.ForShipment(shipmentId, load, StopKind.Pickup, location, sequence: 20, incomingLegDistanceKm: 30, incomingLegTimeTick: 5);

        Assert.NotEqual(Guid.Empty, stop.Id);
        Assert.Equal(shipmentId, stop.ShipmentId);
        Assert.Null(stop.TruckingCompanyId);
        Assert.Equal(StopKind.Pickup, stop.Kind);
        Assert.Equal(StopStatus.Pending, stop.Status);
        Assert.Same(location, stop.Location);
        Assert.Equal(20, stop.Sequence);
        Assert.Equal(30, stop.IncomingLegDistanceKm);
        Assert.Equal(5, stop.IncomingLegTimeTick);
        Assert.Null(stop.ReachedAt);
        Assert.Equal(0, stop.WaitTimeTick);
        Assert.Equal(0, stop.WaitTimeTickElapsed);
        Assert.Same(load, stop.ShipmentLoad);
    }

    [Fact]
    public void ForShipment_EmptyShipmentId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Stop.ForShipment(Guid.Empty, SomeLoad(), StopKind.Pickup, SomeLocation(), 10, 50, 6));
    }

    [Fact]
    public void ForShipment_NullLoad_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Stop.ForShipment(Guid.NewGuid(), null!, StopKind.Pickup, SomeLocation(), 10, 50, 6));
    }

    [Fact]
    public void ForShipment_NullLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Stop.ForShipment(Guid.NewGuid(), SomeLoad(), StopKind.Pickup, null!, 10, 50, 6));
    }

    [Fact]
    public void ForShipment_OfficeKind_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Stop.ForShipment(Guid.NewGuid(), SomeLoad(), StopKind.Office, SomeLocation(), 10, 50, 6));
    }

    [Fact]
    public void ForOffice_ValidInput_SetsProperties()
    {
        var companyId = Guid.NewGuid();
        var location = SomeLocation();

        var stop = Stop.ForOffice(companyId, location, sequence: 10, incomingLegDistanceKm: 5, incomingLegTimeTick: 1);

        Assert.Equal(companyId, stop.TruckingCompanyId);
        Assert.Null(stop.ShipmentId);
        Assert.Equal(StopKind.Office, stop.Kind);
        Assert.Null(stop.ShipmentLoad);
    }

    [Fact]
    public void ForOffice_EmptyCompanyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Stop.ForOffice(Guid.Empty, SomeLocation(), 10, 5, 1));
    }

    [Fact]
    public void ForOffice_NullLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Stop.ForOffice(Guid.NewGuid(), null!, 10, 5, 1));
    }

    [Fact]
    public void MarkReached_PendingStop_SetsStatusAndReachedAt()
    {
        var stop = PickupStop();
        var reachedAt = new DateTime(2026, 1, 1, 10, 0, 0);

        stop.MarkReached(reachedAt);

        Assert.Equal(StopStatus.Reached, stop.Status);
        Assert.Equal(reachedAt, stop.ReachedAt);
    }

    [Fact]
    public void MarkReached_AlreadyReached_Throws()
    {
        var stop = PickupStop();
        stop.MarkReached(new DateTime(2026, 1, 1, 10, 0, 0));

        Assert.Throws<InvalidOperationException>(() => stop.MarkReached(new DateTime(2026, 1, 1, 11, 0, 0)));
    }

    [Fact]
    public void PlanWait_PendingStop_SetsWaitAndZeroesElapsed()
    {
        var stop = PickupStop();

        stop.PlanWait(5);

        Assert.Equal(5, stop.WaitTimeTick);
        Assert.Equal(0, stop.WaitTimeTickElapsed);
    }

    [Fact]
    public void PlanWait_NegativeWait_Throws()
    {
        var stop = PickupStop();

        Assert.Throws<ArgumentOutOfRangeException>(() => stop.PlanWait(-1));
    }

    [Fact]
    public void PlanWait_AlreadyReached_Throws()
    {
        var stop = PickupStop();
        stop.MarkReached(new DateTime(2026, 1, 1, 10, 0, 0));

        Assert.Throws<InvalidOperationException>(() => stop.PlanWait(5));
    }

    [Fact]
    public void PlanWait_RePlanMidProgress_ResetsElapsedToZero()
    {
        var stop = PickupStop();
        stop.PlanWait(10);
        stop.AccrueWait(6);

        stop.PlanWait(3);

        Assert.Equal(3, stop.WaitTimeTick);
        Assert.Equal(0, stop.WaitTimeTickElapsed);
    }

    [Fact]
    public void AccrueWait_PartialTicks_AccumulatesElapsed()
    {
        var stop = PickupStop();
        stop.PlanWait(10);

        stop.AccrueWait(4);

        Assert.Equal(4, stop.WaitTimeTickElapsed);
        Assert.False(stop.IsWaitComplete);
    }

    [Fact]
    public void AccrueWait_ExactlyAtCap_CompletesWait()
    {
        var stop = PickupStop();
        stop.PlanWait(10);

        stop.AccrueWait(10);

        Assert.Equal(10, stop.WaitTimeTickElapsed);
        Assert.True(stop.IsWaitComplete);
    }

    [Fact]
    public void AccrueWait_BeyondCap_ClampsAtWaitTimeTick()
    {
        var stop = PickupStop();
        stop.PlanWait(10);

        stop.AccrueWait(15);

        Assert.Equal(10, stop.WaitTimeTickElapsed);
        Assert.True(stop.IsWaitComplete);
    }

    [Fact]
    public void AccrueWait_NegativeTicks_Throws()
    {
        var stop = PickupStop();
        stop.PlanWait(10);

        Assert.Throws<ArgumentOutOfRangeException>(() => stop.AccrueWait(-1));
    }

    [Fact]
    public void IsWaitComplete_NoWaitPlanned_IsTrueByDefault()
    {
        var stop = PickupStop();

        Assert.Equal(0, stop.WaitTimeTick);
        Assert.True(stop.IsWaitComplete);
    }

    [Fact]
    public void ReplaceIncomingLeg_UpdatesDistanceAndTime()
    {
        var stop = PickupStop();

        stop.ReplaceIncomingLeg(incomingLegDistanceKm: 99, incomingLegTimeTick: 12);

        Assert.Equal(99, stop.IncomingLegDistanceKm);
        Assert.Equal(12, stop.IncomingLegTimeTick);
    }

    [Fact]
    public void Renumber_UpdatesSequence()
    {
        var stop = PickupStop();

        stop.Renumber(999);

        Assert.Equal(999, stop.Sequence);
    }

    [Fact]
    public void Clone_MutatingCloneStatus_DoesNotAffectOriginal()
    {
        var original = PickupStop();

        var clone = original.Clone();
        clone.MarkReached(new DateTime(2026, 1, 1, 10, 0, 0));

        Assert.Equal(StopStatus.Pending, original.Status);
        Assert.Null(original.ReachedAt);
        Assert.Equal(StopStatus.Reached, clone.Status);
    }

    [Fact]
    public void Clone_PreservesSameIdAndSharesImmutableReferences()
    {
        var original = PickupStop();

        var clone = original.Clone();

        Assert.Equal(original.Id, clone.Id);
        Assert.Same(original.Location, clone.Location);
        Assert.Same(original.ShipmentLoad, clone.ShipmentLoad);
    }

    [Fact]
    public void Clone_MutatingCloneWait_DoesNotAffectOriginal()
    {
        var original = PickupStop();
        original.PlanWait(10);

        var clone = original.Clone();
        clone.AccrueWait(5);

        Assert.Equal(0, original.WaitTimeTickElapsed);
        Assert.Equal(5, clone.WaitTimeTickElapsed);
    }
}
