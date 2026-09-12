using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class AddDriverHandlerTests
{
    [Fact]
    public async Task AddDriverAsync_ValidRequest_CreatesDriverWithAllFourRuleFieldsWiredCorrectly()
    {
        var drivers = new Mock<IDriverRepository>();
        Driver? addedDriver = null;
        drivers.Setup(d => d.Add(It.IsAny<Driver>())).Callback<Driver>(d => addedDriver = d);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);

        var handler = new AddDriverHandler(unitOfWork.Object);
        var request = new AddDriverRequest(
            "Jane", "Doe",
            DrivingBreakRule.SplitBreak,
            DailyRestRule.ReducedRest,
            WeeklyRestRule.ReducedWeeklyRest,
            ExtendDailyDrivingWhenEligible: true);

        var response = await handler.AddDriverAsync(request);

        Assert.NotNull(addedDriver);
        Assert.Equal("Jane", addedDriver!.FirstName);
        Assert.Equal("Doe", addedDriver.LastName);
        Assert.Equal(DrivingBreakRule.SplitBreak, addedDriver.Rules.BreakRule);
        Assert.Equal(DailyRestRule.ReducedRest, addedDriver.Rules.DailyRestRule);
        Assert.Equal(WeeklyRestRule.ReducedWeeklyRest, addedDriver.Rules.WeeklyRestRule);
        Assert.True(addedDriver.Rules.ExtendDailyDrivingWhenEligible);

        Assert.Equal(addedDriver.Id, response.DriverId);
        Assert.Equal("Jane", response.FirstName);
        Assert.Equal("Doe", response.LastName);
        Assert.Equal(DrivingBreakRule.SplitBreak, response.BreakRule);
        Assert.Equal(DailyRestRule.ReducedRest, response.DailyRestRule);
        Assert.Equal(WeeklyRestRule.ReducedWeeklyRest, response.WeeklyRestRule);
        Assert.True(response.ExtendDailyDrivingWhenEligible);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddDriverAsync_BlankFirstName_PropagatesDomainValidationUnwrapped()
    {
        var drivers = new Mock<IDriverRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);

        var handler = new AddDriverHandler(unitOfWork.Object);
        var request = new AddDriverRequest(
            "", "Doe", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.AddDriverAsync(request));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
