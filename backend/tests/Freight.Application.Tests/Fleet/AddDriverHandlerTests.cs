using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class AddDriverHandlerTests
{
    private static readonly TruckingCompany Company =
        TruckingCompany.Create(Guid.NewGuid(), "Nordwest Spedition", GeoLocation.Create(53.55, 9.99), "Office place");

    private static (Mock<IUnitOfWork> UnitOfWork, Mock<IDriverRepository> Drivers) SetUp(TruckingCompany? company)
    {
        var drivers = new Mock<IDriverRepository>();
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(company);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);
        return (unitOfWork, drivers);
    }

    [Fact]
    public async Task AddDriverAsync_ValidRequest_CreatesDriverWithAllFourRuleFieldsWiredCorrectly()
    {
        var (unitOfWork, drivers) = SetUp(Company);
        Driver? addedDriver = null;
        drivers.Setup(d => d.Add(It.IsAny<Driver>())).Callback<Driver>(d => addedDriver = d);

        var handler = new AddDriverHandler(unitOfWork.Object);
        var request = new AddDriverRequest(
            "Jane", "Doe",
            DrivingBreakRule.SplitBreak,
            DailyRestRule.ReducedRest,
            WeeklyRestRule.ReducedWeeklyRest,
            ExtendDailyDrivingWhenEligible: true,
            TruckingCompanyId: Company.Id);

        var response = await handler.AddDriverAsync(request);

        Assert.NotNull(addedDriver);
        Assert.Equal("Jane", addedDriver!.FirstName);
        Assert.Equal("Doe", addedDriver.LastName);
        Assert.Equal(DrivingBreakRule.SplitBreak, addedDriver.Rules.BreakRule);
        Assert.Equal(DailyRestRule.ReducedRest, addedDriver.Rules.DailyRestRule);
        Assert.Equal(WeeklyRestRule.ReducedWeeklyRest, addedDriver.Rules.WeeklyRestRule);
        Assert.True(addedDriver.Rules.ExtendDailyDrivingWhenEligible);
        Assert.Equal(Company.Id, addedDriver.TruckingCompanyId);

        Assert.Equal(addedDriver.Id, response.DriverId);
        Assert.Equal("Jane", response.FirstName);
        Assert.Equal("Doe", response.LastName);
        Assert.Equal(DrivingBreakRule.SplitBreak, response.BreakRule);
        Assert.Equal(DailyRestRule.ReducedRest, response.DailyRestRule);
        Assert.Equal(WeeklyRestRule.ReducedWeeklyRest, response.WeeklyRestRule);
        Assert.True(response.ExtendDailyDrivingWhenEligible);
        Assert.Equal(Company.Id, response.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddDriverAsync_UnknownCompany_Throws_NeverAddsOrSaves()
    {
        var (unitOfWork, drivers) = SetUp(company: null);

        var handler = new AddDriverHandler(unitOfWork.Object);
        var request = new AddDriverRequest(
            "Jane", "Doe", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.AddDriverAsync(request));
        drivers.Verify(d => d.Add(It.IsAny<Driver>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDriverAsync_BlankFirstName_PropagatesDomainValidationUnwrapped()
    {
        var (unitOfWork, _) = SetUp(Company);

        var handler = new AddDriverHandler(unitOfWork.Object);
        var request = new AddDriverRequest(
            "", "Doe", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false, Company.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.AddDriverAsync(request));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
