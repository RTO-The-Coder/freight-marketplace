using Freight.Domain.Simulation;
using Freight.Domain.Simulation.Abstractions;
using Moq;

namespace Freight.Application.Tests;

/// <summary>
/// Wires a <see cref="Mock{IUnitOfWork}"/>'s <c>SimulationClock</c> repository to a real,
/// already-existing <see cref="SimulationClock"/> seeded at <paramref name="startingAt"/> -
/// for the (common) case where a test doesn't care about the lazy-create path itself, just
/// wants a clock already in place. <c>GetOrCreateAsync</c> and <c>GetAsync</c> both return
/// the SAME instance on every call, so a handler's <c>AdvanceBy</c>/<c>SetTo</c> mutation is
/// visible to a later call within the same test.
/// </summary>
internal static class FakeSimulationClock
{
    public static SimulationClock SetUp(Mock<Freight.Domain.Common.IUnitOfWork> unitOfWork, DateTime startingAt)
    {
        var clock = SimulationClock.Create(startingAt);
        var repo = new Mock<ISimulationClockRepository>();

        repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(clock);
        repo.Setup(r => r.GetOrCreateAsync(It.IsAny<Func<DateTime>>(), It.IsAny<CancellationToken>())).ReturnsAsync(clock);

        unitOfWork.SetupGet(u => u.SimulationClock).Returns(repo.Object);

        return clock;
    }
}
