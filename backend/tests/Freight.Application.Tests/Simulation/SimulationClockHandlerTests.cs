using Freight.Application.Simulation;
using Freight.Domain.Common;
using Freight.Domain.Simulation;
using Freight.Domain.Simulation.Abstractions;
using Moq;

namespace Freight.Application.Tests.Simulation;

public sealed class SimulationClockHandlerTests
{
    [Fact]
    public async Task GetTimeAsync_NoClockYet_SeedsFromTimeProviderThenReturnsIt()
    {
        var seedTimeOffset = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var repo = new Mock<ISimulationClockRepository>();
        repo.Setup(r => r.GetOrCreateAsync(It.IsAny<Func<DateTime>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<DateTime> seed, CancellationToken _) => SimulationClock.Create(seed()));
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.SimulationClock).Returns(repo.Object);

        var handler = new SimulationClockHandler(unitOfWork.Object, new Freight.Application.Tests.FakeTimeProvider(seedTimeOffset));
        var response = await handler.GetTimeAsync();

        Assert.Equal(seedTimeOffset.UtcDateTime, response.CurrentTime);
    }

    [Fact]
    public async Task GetTimeAsync_ExistingClock_ReturnsItsCurrentTimeWithoutReseeding()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var existing = Freight.Application.Tests.FakeSimulationClock.SetUp(unitOfWork, new DateTime(2026, 1, 1, 6, 0, 0));

        var handler = new SimulationClockHandler(unitOfWork.Object, new Freight.Application.Tests.FakeTimeProvider(DateTimeOffset.UtcNow));
        var response = await handler.GetTimeAsync();

        Assert.Equal(existing.CurrentTime, response.CurrentTime);
    }

    [Fact]
    public async Task SetTimeAsync_MovesTimeBackward_Succeeds()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var clock = Freight.Application.Tests.FakeSimulationClock.SetUp(unitOfWork, new DateTime(2026, 1, 1, 12, 0, 0));

        var handler = new SimulationClockHandler(unitOfWork.Object, new Freight.Application.Tests.FakeTimeProvider(DateTimeOffset.UtcNow));
        var earlier = new DateTime(2026, 1, 1, 6, 0, 0);
        var response = await handler.SetTimeAsync(new SetSimulationTimeRequest(earlier));

        Assert.Equal(earlier, response.CurrentTime);
        Assert.Equal(earlier, clock.CurrentTime);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTimeAsync_MovesTimeForward_Succeeds()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var clock = Freight.Application.Tests.FakeSimulationClock.SetUp(unitOfWork, new DateTime(2026, 1, 1, 6, 0, 0));

        var handler = new SimulationClockHandler(unitOfWork.Object, new Freight.Application.Tests.FakeTimeProvider(DateTimeOffset.UtcNow));
        var later = new DateTime(2026, 1, 1, 12, 0, 0);
        await handler.SetTimeAsync(new SetSimulationTimeRequest(later));

        Assert.Equal(later, clock.CurrentTime);
    }

    [Fact]
    public async Task AdvanceAsync_PositiveMinutes_AdvancesClockAndSaves()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var startTime = new DateTime(2026, 1, 1, 6, 0, 0);
        var clock = Freight.Application.Tests.FakeSimulationClock.SetUp(unitOfWork, startTime);

        var handler = new SimulationClockHandler(unitOfWork.Object, new Freight.Application.Tests.FakeTimeProvider(DateTimeOffset.UtcNow));
        var response = await handler.AdvanceAsync(new AdvanceSimulationTimeRequest(30));

        Assert.Equal(startTime.AddMinutes(30), response.CurrentTime);
        Assert.Equal(startTime.AddMinutes(30), clock.CurrentTime);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdvanceAsync_NegativeMinutes_Throws_NeverSaves()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        Freight.Application.Tests.FakeSimulationClock.SetUp(unitOfWork, new DateTime(2026, 1, 1, 6, 0, 0));

        var handler = new SimulationClockHandler(unitOfWork.Object, new Freight.Application.Tests.FakeTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.AdvanceAsync(new AdvanceSimulationTimeRequest(-1)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
