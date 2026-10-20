using Freight.Domain.Common;

namespace Freight.Application.Tracking;

/// <summary>Completed trips whose <c>CompletedAt</c> falls in [<see cref="From"/>, <see cref="To"/>), optionally for one truck.</summary>
public sealed record GetTrackingHistoryRequest(Guid TruckingCompanyId, DateTime From, DateTime To, Guid? TruckId);

/// <summary>A completed trip: who drove it, its totals, its shipments and what it earned (accepted-offer prices; direct bookings have none).</summary>
public sealed record HistoryTripDto(
    Guid TripId,
    Guid TruckId,
    string TruckName,
    IReadOnlyList<TrackingDriverDto> Drivers,
    DateTime StartedAt,
    DateTime CompletedAt,
    double DistanceKm,
    int DrivingTicks,
    int RestTicks,
    IReadOnlyList<TrackedShipmentDto> Shipments,
    decimal EarnedEur);

/// <summary>
/// Totals over the period's completed trips. <see cref="OnTimePercent"/> is the share of
/// delivered shipments with no late stop - null when nothing was delivered.
/// </summary>
public sealed record HistorySummaryDto(
    int CompletedTrips,
    int ShipmentsDelivered,
    double TonnesCarried,
    decimal EarnedEur,
    int? OnTimePercent,
    int LateCount,
    double DistanceKm);

public sealed record TrackingHistoryResponse(
    DateTime From,
    DateTime To,
    HistorySummaryDto Summary,
    IReadOnlyList<HistoryTripDto> Trips);

/// <summary>
/// The Tracking page's History tab: the company's trips that completed in the period, newest
/// first, each with its drivers (as recorded on the trip), totals and shipments, plus a summary.
/// </summary>
public sealed class GetTrackingHistoryHandler(IUnitOfWork unitOfWork)
{
    public async Task<TrackingHistoryResponse> GetHistoryAsync(GetTrackingHistoryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.To <= request.From)
        {
            throw new ArgumentException("The period's end must be after its start.", nameof(request));
        }

        var companyId = request.TruckingCompanyId;
        _ = await unitOfWork.TruckingCompanies.GetByIdAsync(companyId, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{companyId}' was not found.");

        var trips = await unitOfWork.Trips.GetCompletedByCompanyAsync(
            companyId, request.From, request.To, request.TruckId, cancellationToken);

        var truckNames = (await unitOfWork.Trucks.GetByTruckingCompanyIdAsync(companyId, cancellationToken))
            .ToDictionary(truck => truck.Id, truck => truck.TruckName);
        foreach (var truckId in trips.Select(trip => trip.TruckId).Distinct().Where(id => !truckNames.ContainsKey(id)))
        {
            // The truck has since left the company - its trips still belong to this history.
            if (await unitOfWork.Trucks.GetByIdAsync(truckId, cancellationToken) is { } truck)
            {
                truckNames[truckId] = truck.TruckName;
            }
        }

        var shipmentIds = trips
            .SelectMany(trip => trip.Stops)
            .Where(stop => stop.ShipmentId is not null)
            .Select(stop => stop.ShipmentId!.Value)
            .ToHashSet();
        var shipments = (await unitOfWork.Shipments.GetByIdsAsync(shipmentIds, cancellationToken))
            .ToDictionary(shipment => shipment.Id);
        var shipperNames = await TrackedShipments.ShipperNamesAsync(unitOfWork, shipments.Values, cancellationToken);
        var prices = TrackedShipments.Prices(await unitOfWork.ShipmentOffers.GetByCompanyIdAsync(companyId, cancellationToken));
        var noEtas = new Dictionary<Guid, DateTime>();

        var tripDtos = trips
            .Select(trip =>
            {
                var rows = TrackedShipments.Build(trip, shipments, shipperNames, prices, noEtas);
                return new HistoryTripDto(
                    trip.Id,
                    trip.TruckId,
                    truckNames.GetValueOrDefault(trip.TruckId) ?? string.Empty,
                    TrackedShipments.Drivers(trip),
                    trip.StartedAt,
                    trip.CompletedAt!.Value,
                    trip.DistanceTravelledSoFar,
                    trip.TimeElapsedSoFar,
                    trip.RestTimeSoFar,
                    rows,
                    rows.Sum(row => row.PriceEur ?? 0));
            })
            .ToList();

        var delivered = tripDtos.SelectMany(trip => trip.Shipments).Where(row => row.State == TrackedShipmentState.Delivered).ToList();
        var late = delivered.Count(row => row.LateByMinutes > 0);

        var summary = new HistorySummaryDto(
            tripDtos.Count,
            delivered.Count,
            delivered.Sum(row => row.LoadWeightKg) / 1000,
            tripDtos.Sum(trip => trip.EarnedEur),
            delivered.Count == 0 ? null : (int)Math.Round(100.0 * (delivered.Count - late) / delivered.Count),
            late,
            tripDtos.Sum(trip => trip.DistanceKm));

        return new TrackingHistoryResponse(request.From, request.To, summary, tripDtos);
    }
}
