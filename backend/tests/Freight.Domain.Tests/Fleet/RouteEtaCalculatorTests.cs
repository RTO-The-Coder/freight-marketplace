using Freight.Domain.Fleet;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests.Fleet;

/// <summary>
/// Exercises the forward route walk against the real <see cref="DriverRuleEngine"/> (it
/// is deterministic, and the point of these tests is the interaction between the walk and
/// the actual driving-hours rules, not a stubbed approximation of them).
///
/// Tick = 5 minutes. Default limits: 4.5h (270 min) continuous driving triggers a 45-min
/// break; 9h (540 min) daily driving triggers an 11h (660 min) daily rest.
/// </summary>
public class RouteEtaCalculatorTests
{
    private static readonly IDriverRuleEngine Engine = new DriverRuleEngine();
    private static readonly DateTime Start = new(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc); // a Monday

    private static readonly GeoLocation PickupLocation = GeoLocation.Create(52.5, 13.4);
    private static readonly GeoLocation DeliveryLocation = GeoLocation.Create(48.1, 11.6);
    private static readonly GeoLocation OfficeLocation = GeoLocation.Create(52.52, 13.405);

    private static readonly DrivingRules FullRule =
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false);

    private static RouteEtaCalculator NewCalculator() => new(Engine);

    private static DriverComplianceState FullyRestedLedger() => new(Guid.NewGuid(), Start);

    /// <summary>
    /// A trip with a single shipment: Pickup -> Delivery -> Office. Each incoming leg is
    /// <paramref name="legTicks"/> ticks / <paramref name="legKm"/> km.
    /// </summary>
    private static Trip TripWithOneShipment(int legTicks, double legKm = 100)
    {
        var trip = Trip.Open(Guid.NewGuid(), Guid.NewGuid(), Start);
        trip.AssignShipment(
            Guid.NewGuid(), Capacity.Create(100, 2),
            PickupLocation, DeliveryLocation, OfficeLocation,
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            legKm, legTicks, legKm, legTicks, legKm, legTicks);
        return trip;
    }

    private static Stop StopOfKind(Trip trip, StopKind kind) => trip.Stops.Single(s => s.Kind == kind);

    [Fact]
    public void CalculateEtas_ShortLegsNoBreak_ArrivalIsPureLegTime()
    {
        // Three 12-tick (1h) legs, well under the 4.5h break trigger even summed.
        var trip = TripWithOneShipment(legTicks: 12);
        var calculator = NewCalculator();

        var etas = calculator.CalculateEtas(trip, null, FullyRestedLedger(), FullRule, Start);

        Assert.Equal(Start.AddHours(1), etas[StopOfKind(trip, StopKind.Pickup).Id]);
        Assert.Equal(Start.AddHours(2), etas[StopOfKind(trip, StopKind.Delivery).Id]);
        Assert.Equal(Start.AddHours(3), etas[StopOfKind(trip, StopKind.Office).Id]);
    }

    [Fact]
    public void CalculateEtas_LegLongerThanBreakTrigger_ArrivalDelayedByOneBreak()
    {
        // First leg 72 ticks (6h) driving > 4.5h trigger -> one 45-min break falls inside it.
        // Arrival = 6h driving + 45m break = 6h45m.
        var trip = TripWithOneShipment(legTicks: 72);
        var calculator = NewCalculator();

        var etas = calculator.CalculateEtas(trip, null, FullyRestedLedger(), FullRule, Start);

        Assert.Equal(Start.AddHours(6).AddMinutes(45), etas[StopOfKind(trip, StopKind.Pickup).Id]);
    }

    [Fact]
    public void CalculateEtas_PartwayThroughFirstLeg_RemainingLegTimeOnly()
    {
        // 12-tick leg, already 8 ticks driven -> only 4 ticks (20 min) left to the pickup.
        var trip = TripWithOneShipment(legTicks: 12);
        var progress = new RouteProgress(100, 12);
        progress.AdvanceByTicks(8);
        var calculator = NewCalculator();

        var etas = calculator.CalculateEtas(trip, progress, FullyRestedLedger(), FullRule, Start);

        Assert.Equal(Start.AddMinutes(20), etas[StopOfKind(trip, StopKind.Pickup).Id]);
    }

    [Fact]
    public void CalculateEtas_MultiLegBreakSpansLegBoundary_LaterEtasShiftByTheBreak()
    {
        // Two 30-tick (2.5h) legs then a 30-tick office leg. Break triggers at 4.5h -
        // partway through the second leg - so:
        //   Pickup   @ 2.5h  (no break yet)
        //   Delivery @ 5h + 45m break = 5h45m
        //   Office   @ 7.5h + 45m      = 8h15m
        var trip = TripWithOneShipment(legTicks: 30);
        var calculator = NewCalculator();

        var etas = calculator.CalculateEtas(trip, null, FullyRestedLedger(), FullRule, Start);

        Assert.Equal(Start.AddHours(2).AddMinutes(30), etas[StopOfKind(trip, StopKind.Pickup).Id]);
        Assert.Equal(Start.AddHours(5).AddMinutes(45), etas[StopOfKind(trip, StopKind.Delivery).Id]);
        Assert.Equal(Start.AddHours(8).AddMinutes(15), etas[StopOfKind(trip, StopKind.Office).Id]);
    }

    [Fact]
    public void CalculateEtas_LegForcesDailyRest_ArrivalIncludesFullDailyRest()
    {
        // A single 132-tick (11h) leg. The driver can do 9h driving + one 45m break
        // (needed at 4.5h) then hits the 9h daily cap -> 11h daily rest -> 2h more
        // driving to finish the 11h leg.
        //   9h drive + 45m break + 11h daily rest + 2h drive = 22h45m
        var trip = TripWithOneShipment(legTicks: 132);
        var calculator = NewCalculator();

        var etas = calculator.CalculateEtas(trip, null, FullyRestedLedger(), FullRule, Start);

        Assert.Equal(Start.AddHours(22).AddMinutes(45), etas[StopOfKind(trip, StopKind.Pickup).Id]);
    }

    [Fact]
    public void CalculateEtas_NoPendingStops_ReturnsEmpty()
    {
        var trip = Trip.Open(Guid.NewGuid(), Guid.NewGuid(), Start);
        var calculator = NewCalculator();

        var etas = calculator.CalculateEtas(trip, null, FullyRestedLedger(), FullRule, Start);

        Assert.Empty(etas);
    }

    [Fact]
    public void CalculateEtas_DoesNotMutateCallerLedger()
    {
        var trip = TripWithOneShipment(legTicks: 72);
        var ledger = FullyRestedLedger();
        var calculator = NewCalculator();

        calculator.CalculateEtas(trip, null, ledger, FullRule, Start);

        // The walk should operate on its own copy - a fresh ledger stays untouched.
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(Start, ledger.LastEvaluatedSimulatedTime);
    }
}
