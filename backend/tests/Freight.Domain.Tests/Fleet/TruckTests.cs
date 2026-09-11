using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Tests.Fleet.TestSupport;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using static Freight.Domain.Tests.Fleet.TestSupport.TripTestBuilder;

namespace Freight.Domain.Tests.Fleet;

public class TruckTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);

    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false);

    private static Driver SomeDriver() => Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());

    private static Truck SomeTruck(TruckSize size = TruckSize.Medium) =>
        Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, size);

    [Fact]
    public void Create_ValidInput_SetsPropertiesAndDefaults()
    {
        var id = Guid.NewGuid();

        var truck = Truck.Create(id, "Truck-1", TruckType.Refrigerated, TruckSize.Medium);

        Assert.Equal(id, truck.Id);
        Assert.Equal("Truck-1", truck.TruckName);
        Assert.Equal(TruckType.Refrigerated, truck.Type);
        Assert.Equal(TruckSize.Medium, truck.Size);
        Assert.Equal(Capacity.ForTruckSize(TruckSize.Medium), truck.Capacity);
        Assert.Null(truck.TruckingCompanyId);
        Assert.False(truck.IsActive);
        Assert.Null(truck.DriverAssignment);
        Assert.False(truck.HazmatCertified);
        Assert.Null(truck.CurrentProgress);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Truck.Create(Guid.Empty, "Truck-1", TruckType.Refrigerated, TruckSize.Medium));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => Truck.Create(Guid.NewGuid(), name, TruckType.Refrigerated, TruckSize.Medium));
    }

    [Fact]
    public void CertifyForHazmat_SetsFlagTrue()
    {
        var truck = SomeTruck();

        truck.CertifyForHazmat();

        Assert.True(truck.HazmatCertified);
    }

    [Fact]
    public void RevokeHazmatCertification_SetsFlagFalse()
    {
        var truck = SomeTruck();
        truck.CertifyForHazmat();

        truck.RevokeHazmatCertification();

        Assert.False(truck.HazmatCertified);
    }

    [Fact]
    public void AssignToCompany_ValidId_SetsTruckingCompanyId()
    {
        var truck = SomeTruck();
        var companyId = Guid.NewGuid();

        truck.AssignToCompany(companyId);

        Assert.Equal(companyId, truck.TruckingCompanyId);
    }

    [Fact]
    public void AssignToCompany_EmptyId_Throws()
    {
        var truck = SomeTruck();

        Assert.Throws<ArgumentException>(() => truck.AssignToCompany(Guid.Empty));
    }

    [Fact]
    public void UnassignFromCompany_ClearsCompanyAndForcesInactive()
    {
        var truck = SomeTruck();
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(SomeDriver());
        truck.Activate();

        truck.UnassignFromCompany();

        Assert.Null(truck.TruckingCompanyId);
        Assert.False(truck.IsActive);
        // Asymmetric with RemoveDrivers: unassigning the company leaves DriverAssignment untouched.
        Assert.NotNull(truck.DriverAssignment);
    }

    [Fact]
    public void RemoveDrivers_ClearsAssignmentAndForcesInactive()
    {
        var truck = SomeTruck();
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(SomeDriver());
        truck.Activate();

        truck.RemoveDrivers();

        Assert.Null(truck.DriverAssignment);
        Assert.False(truck.IsActive);
        // Asymmetric with UnassignFromCompany: removing drivers leaves TruckingCompanyId untouched.
        Assert.NotNull(truck.TruckingCompanyId);
    }

    [Fact]
    public void Activate_MissingCompany_Throws()
    {
        var truck = SomeTruck();
        truck.AssignDrivers(SomeDriver());

        Assert.Throws<InvalidOperationException>(() => truck.Activate());
    }

    [Fact]
    public void Activate_MissingDriverAssignment_Throws()
    {
        var truck = SomeTruck();
        truck.AssignToCompany(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => truck.Activate());
    }

    [Fact]
    public void Activate_CompanyAndDriverPresent_SetsActiveTrue()
    {
        var truck = SomeTruck();
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(SomeDriver());

        truck.Activate();

        Assert.True(truck.IsActive);
    }

    [Fact]
    public void Deactivate_SetsActiveFalse()
    {
        var truck = SomeTruck();
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(SomeDriver());
        truck.Activate();

        truck.Deactivate();

        Assert.False(truck.IsActive);
    }

    [Fact]
    public void AssignDrivers_SinglePrimaryOnly_CreatesSingleAssignment()
    {
        var truck = SomeTruck();
        var driver = SomeDriver();

        truck.AssignDrivers(driver);

        Assert.Equal(DriverConfigurationType.Single, truck.DriverAssignment!.ConfigurationType);
        Assert.Same(driver, truck.DriverAssignment.PrimaryDriver);
    }

    [Fact]
    public void AssignDrivers_PrimaryAndSecondaryOnLargeTruck_CreatesTeamAssignment()
    {
        var truck = SomeTruck(TruckSize.Large);
        var primary = SomeDriver();
        var secondary = SomeDriver();

        truck.AssignDrivers(primary, secondary);

        Assert.Equal(DriverConfigurationType.Team, truck.DriverAssignment!.ConfigurationType);
    }

    [Fact]
    public void AssignDrivers_NullPrimary_Throws()
    {
        var truck = SomeTruck();

        Assert.Throws<ArgumentNullException>(() => truck.AssignDrivers(null!));
    }

    [Fact]
    public void AssignDrivers_ReplacesExistingAssignmentWithFreshOne()
    {
        var truck = SomeTruck();
        truck.AssignDrivers(SomeDriver());
        var firstAssignment = truck.DriverAssignment;
        var newPrimary = SomeDriver();

        truck.AssignDrivers(newPrimary);

        Assert.NotSame(firstAssignment, truck.DriverAssignment);
        Assert.Same(newPrimary, truck.DriverAssignment!.PrimaryDriver);
        Assert.Equal(newPrimary.Id, truck.DriverAssignment.ActiveDriverId);
    }

    [Fact]
    public void BeginTripCompliance_NoDriverAssignment_Throws()
    {
        var truck = SomeTruck();

        Assert.Throws<InvalidOperationException>(() => truck.BeginTripCompliance(StartedAt));
    }

    [Fact]
    public void BeginTripCompliance_SingleDriver_ResetsPrimaryOnly()
    {
        var truck = SomeTruck();
        var driver = SomeDriver();
        truck.AssignDrivers(driver);

        truck.BeginTripCompliance(StartedAt);

        Assert.NotNull(driver.ComplianceState);
        Assert.Equal(StartedAt, driver.ComplianceState!.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void BeginTripCompliance_TeamDrivers_ResetsBoth()
    {
        var truck = SomeTruck(TruckSize.Large);
        var primary = SomeDriver();
        var secondary = SomeDriver();
        truck.AssignDrivers(primary, secondary);

        truck.BeginTripCompliance(StartedAt);

        Assert.NotNull(primary.ComplianceState);
        Assert.NotNull(secondary.ComplianceState);
    }

    [Fact]
    public void SetActiveDriver_NoDriverAssignment_Throws()
    {
        var truck = SomeTruck();

        Assert.Throws<InvalidOperationException>(() => truck.SetActiveDriver(Guid.NewGuid()));
    }

    [Fact]
    public void SetActiveDriver_ValidDriver_DelegatesToAssignment()
    {
        var truck = SomeTruck(TruckSize.Large);
        var primary = SomeDriver();
        var secondary = SomeDriver();
        truck.AssignDrivers(primary, secondary);

        truck.SetActiveDriver(secondary.Id);

        Assert.Equal(secondary.Id, truck.DriverAssignment!.ActiveDriverId);
    }

    // --- DetermineStatus (parameterless / 1-arg): can never return Parked ---

    [Fact]
    public void DetermineStatus_NoDriverAssignment_ReturnsIdle()
    {
        var truck = SomeTruck();

        Assert.Equal(TruckStatus.Idle, truck.DetermineStatus());
        Assert.Equal(TruckStatus.Idle, truck.Status);
    }

    [Fact]
    public void DetermineStatus_NoCurrentTrip_ReturnsAtOffice()
    {
        var truck = SomeTruck();
        truck.AssignDrivers(SomeDriver());

        Assert.Equal(TruckStatus.AtOffice, truck.DetermineStatus(currentTrip: null));
    }

    [Fact]
    public void DetermineStatus_TripAtOffice_ReturnsAtOffice()
    {
        var truck = SomeTruck();
        truck.AssignDrivers(SomeDriver());
        var (trip, _) = OpenTripWithOneShipment(truckId: truck.Id);
        foreach (var stop in trip.Stops.Where(s => s.Kind != StopKind.Office))
        {
            trip.MarkStopReached(stop.Id, StartedAt.AddHours(1));
        }

        Assert.Equal(TruckStatus.AtOffice, truck.DetermineStatus(trip));
    }

    [Fact]
    public void DetermineStatus_ParameterlessOverload_NeverReturnsParkedEvenWhenWaitDataWouldJustifyIt()
    {
        var truck = SomeTruck();
        truck.AssignDrivers(SomeDriver());
        var (trip, _) = OpenTripWithOneShipment(truckId: truck.Id);
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        // Complete the leg - if the wait-aware overload were used with a future window, this
        // would justify Parked. The 1-arg overload must still report Running, never Parked.
        truck.CurrentProgress!.AdvanceByTicks(pickup.IncomingLegTimeTick);

        Assert.NotEqual(TruckStatus.Parked, truck.DetermineStatus(trip));
        Assert.Equal(TruckStatus.Running, truck.DetermineStatus(trip));
    }

    // --- DetermineStatus (wait-aware overload): Parked needs all 4 conditions ---

    private (Truck Truck, Trip Trip, TimeWindow FutureWindow) SetUpMidLegAtCompleteLeg()
    {
        var truck = SomeTruck();
        truck.AssignDrivers(SomeDriver());
        var (trip, _) = OpenTripWithOneShipment(truckId: truck.Id);
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        truck.CurrentProgress!.AdvanceByTicks(pickup.IncomingLegTimeTick);
        var futureWindow = TimeWindow.Create(StartedAt.AddHours(5), StartedAt.AddHours(6));
        return (truck, trip, futureWindow);
    }

    [Fact]
    public void DetermineStatus_AllFourParkedConditionsMet_ReturnsParked()
    {
        var (truck, trip, futureWindow) = SetUpMidLegAtCompleteLeg();

        var status = truck.DetermineStatus(trip, simulatedNow: StartedAt, nextStopWindow: futureWindow);

        Assert.Equal(TruckStatus.Parked, status);
    }

    [Fact]
    public void DetermineStatus_MissingSimulatedNow_FallsThroughToRunning()
    {
        var (truck, trip, futureWindow) = SetUpMidLegAtCompleteLeg();

        var status = truck.DetermineStatus(trip, simulatedNow: null, nextStopWindow: futureWindow);

        Assert.Equal(TruckStatus.Running, status);
    }

    [Fact]
    public void DetermineStatus_MissingNextStopWindow_FallsThroughToRunning()
    {
        var (truck, trip, _) = SetUpMidLegAtCompleteLeg();

        var status = truck.DetermineStatus(trip, simulatedNow: StartedAt, nextStopWindow: null);

        Assert.Equal(TruckStatus.Running, status);
    }

    [Fact]
    public void DetermineStatus_LegNotComplete_FallsThroughToRunning()
    {
        var truck = SomeTruck();
        truck.AssignDrivers(SomeDriver());
        var (trip, _) = OpenTripWithOneShipment(truckId: truck.Id);
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        // Leg not advanced - IsLegComplete() is false.
        var futureWindow = TimeWindow.Create(StartedAt.AddHours(5), StartedAt.AddHours(6));

        var status = truck.DetermineStatus(trip, simulatedNow: StartedAt, nextStopWindow: futureWindow);

        Assert.Equal(TruckStatus.Running, status);
    }

    [Fact]
    public void DetermineStatus_NowAtOrAfterWindowEarliest_FallsThroughToRunning()
    {
        var (truck, trip, _) = SetUpMidLegAtCompleteLeg();
        var window = TimeWindow.Create(StartedAt, StartedAt.AddHours(1));

        var status = truck.DetermineStatus(trip, simulatedNow: StartedAt, nextStopWindow: window);

        Assert.Equal(TruckStatus.Running, status);
    }

    // --- SyncProgressToNextStop: 3 branches ---

    [Fact]
    public void SyncProgressToNextStop_FirstEverCall_CreatesProgressForNextStop()
    {
        var truck = SomeTruck();
        var (trip, _) = OpenTripWithOneShipment(truckId: truck.Id);
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);

        truck.SyncProgressToNextStop(trip, previousNextStopId: null);

        Assert.NotNull(truck.CurrentProgress);
        Assert.Equal(pickup.IncomingLegDistanceKm, truck.CurrentProgress!.TotalDistanceKm);
        Assert.Equal(pickup.IncomingLegTimeTick, truck.CurrentProgress.TotalTimeTick);
    }

    [Fact]
    public void SyncProgressToNextStop_NextStopUnchanged_LeavesProgressUntouched()
    {
        var truck = SomeTruck();
        var (trip, _) = OpenTripWithOneShipment(truckId: truck.Id);
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        truck.CurrentProgress!.AdvanceByTicks(2);

        truck.SyncProgressToNextStop(trip, previousNextStopId: pickup.Id);

        // Still mid-progress - a fresh leg would have reset CurrentDrivingTimeTick to 0.
        Assert.Equal(2, truck.CurrentProgress.CurrentDrivingTimeTick);
    }

    [Fact]
    public void SyncProgressToNextStop_NextStopChangedMidLeg_BanksExactPartialProgressAndStartsFreshLeg()
    {
        var truck = SomeTruck();
        var (trip, _) = OpenTripWithOneShipment(truckId: truck.Id);
        var originalPickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        truck.CurrentProgress!.AdvanceByTicks(3);
        var bankedDistance = truck.CurrentProgress.CurrentDistanceKm;
        var bankedTime = truck.CurrentProgress.CurrentDrivingTimeTick;

        // Insert a new shipment ahead of the truck's position - the next stop changes.
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, MidRouteLegPlan());
        var newNextStop = trip.NextStop!;
        Assert.NotEqual(originalPickup.Id, newNextStop.Id);

        truck.SyncProgressToNextStop(trip, previousNextStopId: originalPickup.Id);

        Assert.Equal(bankedDistance, trip.DistanceTravelledSoFar);
        Assert.Equal(bankedTime, trip.TimeElapsedSoFar);
        Assert.Equal(0, truck.CurrentProgress.CurrentDrivingTimeTick);
        Assert.Equal(newNextStop.IncomingLegDistanceKm, truck.CurrentProgress.TotalDistanceKm);
    }

    [Fact]
    public void SyncProgressToNextStop_NullTrip_Throws()
    {
        var truck = SomeTruck();

        Assert.Throws<ArgumentNullException>(() => truck.SyncProgressToNextStop(null!, null));
    }

    [Fact]
    public void SyncProgressToNextStop_TripBelongsToDifferentTruck_Throws()
    {
        var truck = SomeTruck();
        var trip = OpenTrip(truckId: Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => truck.SyncProgressToNextStop(trip, null));
    }

    [Fact]
    public void SyncProgressToNextStop_TripHasNoPendingStop_Throws()
    {
        var truck = SomeTruck();
        var trip = OpenTrip(truckId: truck.Id);

        Assert.Throws<InvalidOperationException>(() => truck.SyncProgressToNextStop(trip, null));
    }
}
