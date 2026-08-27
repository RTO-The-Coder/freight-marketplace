using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class AddDriverHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidRequest_AddsDriverWithRulesAndSaves()
    {
        var drivers = new Mock<IDriverRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);

        Driver? addedDriver = null;
        drivers.Setup(d => d.Add(It.IsAny<Driver>())).Callback<Driver>(d => addedDriver = d);

        var handler = new AddDriverHandler(unitOfWork.Object);

        var response = await handler.AddDriver(new AddDriverRequest(
            "Jane",
            "Doe",
            DrivingBreakRule.FullBreak,
            DailyRestRule.FullRest,
            WeeklyRestRule.FullWeeklyRest,
            ExtendDailyDrivingWhenEligible: true));

        Assert.NotNull(addedDriver);
        Assert.Equal("Jane", addedDriver!.FirstName);
        Assert.Equal("Doe", addedDriver.LastName);
        Assert.Equal(DrivingBreakRule.FullBreak, addedDriver.Rules.BreakRule);
        Assert.Equal(response.DriverId, addedDriver.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
