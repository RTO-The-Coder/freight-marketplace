using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Client;

namespace Freight.Api.Tests.Controllers;

public sealed class ShippersControllerTests : ApiTestBase
{
    [Fact]
    public async Task GetShippers_NoneExist_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/shippers");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetShippersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Shippers);
    }

    [Fact]
    public async Task GetShippers_OneSeeded_ReturnsIt()
    {
        var shipper = await Factory.SeedShipperAsync("Acme Shipping", "contact@acme.com");

        var response = await Client.GetAsync("/shippers");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetShippersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body.Shippers, s => s.ShipperId == shipper.Id && s.Name == "Acme Shipping" && s.ContactEmail == "contact@acme.com");
    }

    [Fact]
    public async Task GetShipmentsByShipper_NoShipments_ReturnsEmptyList()
    {
        var shipper = await Factory.SeedShipperAsync();

        var response = await Client.GetAsync($"/shippers/{shipper.Id}/shipments");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetShipmentsByShipperResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Shipments);
    }

    [Fact]
    public async Task GetShipmentsByShipper_UnknownShipper_ReturnsEmptyListRatherThanThrowing()
    {
        // GetShipmentsByShipperHandler never validates the shipper id exists - it just
        // queries shipments by shipper id, which legitimately returns none.
        var response = await Client.GetAsync($"/shippers/{Guid.NewGuid()}/shipments");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetShipmentsByShipperResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Shipments);
    }
}
