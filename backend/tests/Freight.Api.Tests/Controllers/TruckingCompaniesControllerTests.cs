using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Fleet;

namespace Freight.Api.Tests.Controllers;

public sealed class TruckingCompaniesControllerTests : ApiTestBase
{
    [Fact]
    public async Task GetTruckingCompanies_NoneExist_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/companies");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetTruckingCompaniesResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Companies);
    }

    [Fact]
    public async Task GetTruckingCompany_UnknownId_Returns400ViaExceptionMiddleware()
    {
        var response = await Client.GetAsync($"/companies/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetFleetTree_UnknownCompany_ReturnsEmptyTreeRatherThanThrowing()
    {
        // GetFleetTreeHandler never validates the company id exists - it just queries
        // trucks by company id, which legitimately returns none for an unknown id.
        var response = await Client.GetAsync($"/companies/{Guid.NewGuid()}/fleet");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetFleetTreeResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Trucks);
    }

    [Fact]
    public async Task GetTruckingCompanies_OneSeeded_ReturnsIt()
    {
        var company = await Factory.SeedTruckingCompanyAsync("Acme Trucking");

        var response = await Client.GetAsync("/companies");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetTruckingCompaniesResponse>();
        Assert.NotNull(body);
        Assert.Contains(body.Companies, c => c.CompanyId == company.Id && c.Name == "Acme Trucking");
    }

    [Fact]
    public async Task GetTruckingCompany_Seeded_ReturnsMatchingDto()
    {
        var company = await Factory.SeedTruckingCompanyAsync("Acme Trucking");

        var response = await Client.GetAsync($"/companies/{company.Id}");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TruckingCompanySummaryDto>();
        Assert.NotNull(body);
        Assert.Equal(company.Id, body.CompanyId);
        Assert.Equal("Acme Trucking", body.Name);
    }
}
