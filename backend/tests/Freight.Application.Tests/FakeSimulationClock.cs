using Freight.Domain.Common;
using Freight.Domain.Simulation;
using Moq;

namespace Freight.Application.Tests;

/// <summary>
/// Wires a mocked <see cref="IUnitOfWork.SimulationClock"/> to return a
/// <see cref="SimulationClock"/> seeded at <paramref name="currentTime"/>, so handlers
/// that read simulated "now" via <see cref="ISimulationClockRepository.GetOrCreateAsync"/>
/// get a deterministic value in unit tests.
/// </summary>
internal static class FakeSimulationClock
{
    public static SimulationClock SetUp(Mock<IUnitOfWork> unitOfWork, DateTime currentTime)
    {
        var clock = SimulationClock.Create(currentTime);

        var repo = new Mock<ISimulationClockRepository>();
        repo.Setup(r => r.GetOrCreateAsync(It.IsAny<Func<DateTime>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clock);
        repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(clock);

        unitOfWork.SetupGet(u => u.SimulationClock).Returns(repo.Object);

        return clock;
    }
}
