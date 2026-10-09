using Freight.Application.Client;
using Freight.Application.Evaluation;
using Freight.Application.Fleet;
using Freight.Application.Offers;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Notifications.Abstractions;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Integration.TestSupport;

/// <summary>
/// In-memory world for offer tests: real handlers, the real eligibility engine and insertion
/// planner, a <see cref="FakeUnitOfWork"/> and a fixed-leg <see cref="FakeRoutingService"/>.
/// The simulation clock starts at <see cref="ClockStart"/> on the first booking.
/// </summary>
internal sealed class OfferScenario
{
    public static readonly DateTime ClockStart = new(2026, 1, 1, 6, 0, 0);

    public FakeUnitOfWork UnitOfWork { get; } = new();
    public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(ClockStart, TimeSpan.Zero));
    public FakeRoutingService Routing { get; } = new() { DefaultLeg = new Domain.Routing.Abstractions.RouteLeg(20, 6) };
    public Mock<INotificationSender> Sender { get; } = new();

    private int _truckCount;

    /// <summary>A company with one active Refrigerated truck that has a driver - eligible for <see cref="BookOpenShipmentAsync"/>.</summary>
    public async Task<(Guid CompanyId, Guid TruckId)> AddCompanyWithTruckAsync(string name = "Acme Trucking")
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), name, GeoLocation.Create(50.11, 8.68));
        UnitOfWork.TruckingCompaniesRepo.Add(company);
        var truckId = await AddTruckAsync(company.Id);
        return (company.Id, truckId);
    }

    public async Task<Guid> AddTruckAsync(Guid companyId)
    {
        _truckCount++;
        var truck = await new AddTruckHandler(UnitOfWork).AddTruckAsync(
            new AddTruckRequest($"Truck-{_truckCount}", TruckType.Refrigerated, TruckSize.Medium, companyId));
        var driver = await new AddDriverHandler(UnitOfWork).AddDriverAsync(
            new AddDriverRequest($"Driver{_truckCount}", "Doe", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));
        await new AssignDriversHandler(UnitOfWork).AssignDriversAsync(new AssignDriversRequest(truck.TruckId, driver.DriverId, null));
        await new SetTruckActivationHandler(UnitOfWork).SetTruckActivationAsync(new SetTruckActivationRequest(truck.TruckId, true));
        return truck.TruckId;
    }

    public async Task<Guid> BookOpenShipmentAsync(Guid? directCompanyId = null)
    {
        var shipper = Domain.Client.Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        UnitOfWork.ShippersRepo.Add(shipper);
        var response = await new BookShipmentHandler(UnitOfWork, TimeProvider, Sender.Object).BookShipmentAsync(new BookShipmentRequest(
            shipper.Id, GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
            Capacity.Create(50, 1), TruckType.Refrigerated,
            TimeWindow.Create(ClockStart, ClockStart.AddDays(2)), TimeWindow.Create(ClockStart, ClockStart.AddDays(3)),
            directCompanyId));
        return response.ShipmentId;
    }

    public async Task AdvanceClockAsync(TimeSpan elapsed)
    {
        var clock = await UnitOfWork.SimulationClock.GetOrCreateAsync(() => ClockStart);
        clock.AdvanceBy(elapsed);
    }

    public Task<SendOffersResponse> SendAsync(Guid companyId, Guid shipmentId, params OfferItem[] offers) =>
        SendOffers().SendOffersAsync(new SendOffersRequest(companyId, shipmentId, offers));

    public SendOffersHandler SendOffers() => new(UnitOfWork, EvaluationEngine(), TimeProvider);

    public AcceptOfferHandler Accept() => new(UnitOfWork, TimeProvider);

    public AddOfferToTripHandler AddToTrip() => new(UnitOfWork, AssignHandler());

    public GetCompanyShipmentBoardHandler Board() => new(UnitOfWork, TimeProvider);

    public GetShipmentOffersHandler ShipmentOffers() => new(UnitOfWork, TimeProvider);

    public AssignShipmentToTruckHandler AssignHandler() => new(UnitOfWork, Planner());

    private ShipmentEvaluationEngine EvaluationEngine() =>
        new(UnitOfWork, Planner(), new RouteEtaCalculator(new DriverRuleEngine()), TimeProvider);

    private ShipmentInsertionPlanner Planner() =>
        new(UnitOfWork, new ShipmentInsertionEvaluator(new RouteEtaCalculator(new DriverRuleEngine())), Routing, TimeProvider);
}
