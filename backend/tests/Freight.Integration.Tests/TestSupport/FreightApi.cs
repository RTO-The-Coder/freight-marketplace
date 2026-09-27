using System.Net.Http.Json;
using System.Text.Json;
using Freight.Api.Controllers;
using Freight.Application.Client;
using Freight.Application.Fleet;
using Freight.Application.Simulation;
using Freight.Application.Tracking;

namespace Freight.Integration.Tests.TestSupport;

/// <summary>
/// Typed calls to the in-process API, so scenarios read as steps rather than HTTP plumbing.
/// Any non-success response throws with the status and the API's error body, so a failed
/// step says why (e.g. the fake router naming a leg missing from the route table).
/// </summary>
public sealed class FreightApi(HttpClient client, JsonSerializerOptions jsonOptions)
{
    public Task SetClockAsync(DateTime time) =>
        SendAsync(HttpMethod.Post, "/simulation/time", new SetSimulationTimeBody(time));

    public Task<AdvanceSimulationResponse> AdvanceAsync(int ticks) =>
        SendAsync<AdvanceSimulationResponse>(HttpMethod.Post, "/simulation/advance", new AdvanceSimulationBody(ticks));

    public async Task<Guid> AddTruckAsync(AddTruckBody body) =>
        (await SendAsync<AddTruckResponse>(HttpMethod.Post, "/trucks", body)).TruckId;

    public Task AssignTruckToCompanyAsync(Guid truckId, Guid companyId) =>
        SendAsync(HttpMethod.Post, $"/trucks/{truckId}/company", new AssignTruckToCompanyBody(companyId));

    public async Task<Guid> AddDriverAsync(AddDriverBody body) =>
        (await SendAsync<AddDriverResponse>(HttpMethod.Post, "/drivers", body)).DriverId;

    public Task AssignDriversAsync(Guid truckId, Guid primaryDriverId, Guid? secondaryDriverId = null) =>
        SendAsync(HttpMethod.Patch, $"/trucks/{truckId}/drivers", new AssignDriversBody(primaryDriverId, secondaryDriverId));

    public Task ActivateTruckAsync(Guid truckId) =>
        SendAsync(HttpMethod.Post, $"/trucks/{truckId}/activate", body: null);

    public async Task<Guid> BookShipmentAsync(BookShipmentBody body) =>
        (await SendAsync<BookShipmentResponse>(HttpMethod.Post, "/shipments", body)).ShipmentId;

    /// <param name="tripStartTime">Planned departure, for the shipment that opens a trip; null departs now.</param>
    public Task AssignShipmentAsync(
        Guid truckId, Guid shipmentId, int pickupInsertIndex, int deliveryInsertIndex, DateTime? tripStartTime = null) =>
        SendAsync(
            HttpMethod.Post,
            $"/trucks/{truckId}/assign-shipment",
            new AssignShipmentToTruckBody(shipmentId, pickupInsertIndex, deliveryInsertIndex, tripStartTime));

    public Task<DriverDetailDto> GetDriverAsync(Guid driverId) =>
        SendAsync<DriverDetailDto>(HttpMethod.Get, $"/drivers/{driverId}", body: null);

    public Task<TruckDetailDto> GetTruckAsync(Guid truckId) =>
        SendAsync<TruckDetailDto>(HttpMethod.Get, $"/trucks/{truckId}", body: null);

    public Task<TruckEtasDto> GetTruckEtasAsync(Guid truckId) =>
        SendAsync<TruckEtasDto>(HttpMethod.Get, $"/trucks/{truckId}/etas", body: null);

    public Task<TruckPositionDto> GetTruckPositionAsync(Guid truckId) =>
        SendAsync<TruckPositionDto>(HttpMethod.Get, $"/trucks/{truckId}/position", body: null);

    public Task<GetShipmentsByShipperResponse> GetShipperShipmentsAsync(Guid shipperId) =>
        SendAsync<GetShipmentsByShipperResponse>(HttpMethod.Get, $"/shippers/{shipperId}/shipments", body: null);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body)
    {
        using var response = await SendRawAsync(method, path, body);
        return (await response.Content.ReadFromJsonAsync<T>(jsonOptions))
            ?? throw new InvalidOperationException($"{method} {path} returned an empty body.");
    }

    private async Task SendAsync(HttpMethod method, string path, object? body)
    {
        using var response = await SendRawAsync(method, path, body);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: jsonOptions)
        };

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            response.Dispose();
            throw new HttpRequestException($"{method} {path} failed with {(int)response.StatusCode}: {error}");
        }

        return response;
    }
}
