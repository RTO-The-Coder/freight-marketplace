using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Simulation;

namespace Freight.Api.Tests.Controllers;

public sealed class SimulationControllerTests : ApiTestBase
{
    [Fact]
    public async Task GetTime_NoClockRowYet_CreatesOneAndReturnsCurrentRealUtcTime()
    {
        var before = DateTime.UtcNow;

        var response = await Client.GetAsync("/simulation/time");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SimulationTimeResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body.CurrentTime >= before.AddSeconds(-5) && body.CurrentTime <= DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task SetTime_ThenGetTime_PersistsAndCanMoveBackward()
    {
        var newTime = new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

        var setResponse = await Client.PostAsJsonAsync("/simulation/time", new { NewCurrentTime = newTime }, JsonOptions);
        setResponse.EnsureSuccessStatusCode();
        var setBody = await setResponse.Content.ReadFromJsonAsync<SimulationTimeResponse>(JsonOptions);
        Assert.Equal(newTime, setBody!.CurrentTime);

        // SimulationClock.SetTo allows moving the clock backward with no guard.
        var earlier = newTime.AddDays(-10);
        var backwardResponse = await Client.PostAsJsonAsync("/simulation/time", new { NewCurrentTime = earlier }, JsonOptions);
        backwardResponse.EnsureSuccessStatusCode();
        var backwardBody = await backwardResponse.Content.ReadFromJsonAsync<SimulationTimeResponse>(JsonOptions);
        Assert.Equal(earlier, backwardBody!.CurrentTime);

        var getResponse = await Client.GetFromJsonAsync<SimulationTimeResponse>("/simulation/time", JsonOptions);
        Assert.Equal(earlier, getResponse!.CurrentTime);
    }

    [Fact]
    public async Task Advance_ZeroTicks_ReturnsSuccessWithNoTimeChange()
    {
        var setTime = new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;
        await Client.PostAsJsonAsync("/simulation/time", new { NewCurrentTime = setTime }, JsonOptions);

        var response = await Client.PostAsJsonAsync("/simulation/advance", new { Ticks = 0 }, JsonOptions);

        response.EnsureSuccessStatusCode();
        var getResponse = await Client.GetFromJsonAsync<SimulationTimeResponse>("/simulation/time", JsonOptions);
        Assert.Equal(setTime, getResponse!.CurrentTime);
    }
}
