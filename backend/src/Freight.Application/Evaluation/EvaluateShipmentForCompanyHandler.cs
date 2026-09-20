namespace Freight.Application.Evaluation;

public sealed record EvaluateShipmentForCompanyRequest(Guid ShipmentId, Guid TruckingCompanyId);

public sealed record EvaluateShipmentForCompanyResponse(IReadOnlyList<TruckEvaluationResult> Trucks);

/// <summary>
/// Thin application-layer wrapper around <see cref="ShipmentEvaluationEngine"/> so it's
/// reachable the same way every other use case is - a controller calling a "Handler"
/// (auto-registered by <see cref="Freight.Application.DependencyInjection.AddApplication"/>),
/// not a raw domain service. See docs/adr/0012-shipment-evaluation-insertion-search.md.
/// </summary>
public sealed class EvaluateShipmentForCompanyHandler(ShipmentEvaluationEngine evaluationEngine)
{
    public async Task<EvaluateShipmentForCompanyResponse> EvaluateAsync(
        EvaluateShipmentForCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var results = await evaluationEngine.EvaluateForCompanyAsync(
            request.ShipmentId, request.TruckingCompanyId, cancellationToken);
        return new EvaluateShipmentForCompanyResponse(results);
    }
}
