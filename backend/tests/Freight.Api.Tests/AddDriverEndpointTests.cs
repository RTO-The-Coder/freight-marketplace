using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects.RuleVariants;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freight.Api.Tests;

public sealed class AddDriverEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public AddDriverEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record AddDriverBody(
        string FirstName,
        string LastName,
        DrivingBreakRule BreakRule,
        DailyRestRule DailyRestRule,
        WeeklyRestRule WeeklyRestRule,
        bool ExtendDailyDrivingWhenEligible);

    private sealed record AddDriverResponse(Guid DriverId);

    [Fact]
    public async Task PostDriver_ValidRequest_Returns201WithPersistedDriver()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/drivers",
            new AddDriverBody("Integration", "Driver", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, true));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<AddDriverResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.DriverId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await dbContext.Set<Driver>().FirstAsync(d => d.Id == body.DriverId);
        Assert.Equal("Integration", persisted.FirstName);
        Assert.Equal(DrivingBreakRule.FullBreak, persisted.Rules.BreakRule);
    }
}
