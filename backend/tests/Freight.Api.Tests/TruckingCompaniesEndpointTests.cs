using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freight.Api.Tests;

public sealed class TruckingCompaniesEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TruckingCompaniesEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record CompanySummary(Guid CompanyId, string Name, double OfficeLatitude, double OfficeLongitude);

    [Fact]
    public async Task GetById_ExistingCompany_ReturnsNameAndOfficeLocation()
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), "Wroclaw Freight", GeoLocation.Create(51.11, 17.03));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<TruckingCompany>().Add(company);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var body = await client.GetFromJsonAsync<CompanySummary>($"/companies/{company.Id}");

        Assert.NotNull(body);
        Assert.Equal(company.Id, body!.CompanyId);
        Assert.Equal("Wroclaw Freight", body.Name);
        Assert.Equal(51.11, body.OfficeLatitude, precision: 5);
        Assert.Equal(17.03, body.OfficeLongitude, precision: 5);
    }

    [Fact]
    public async Task GetById_UnknownCompany_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/companies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
