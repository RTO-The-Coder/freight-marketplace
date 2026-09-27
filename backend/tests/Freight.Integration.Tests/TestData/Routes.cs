using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestSupport;

namespace Freight.Integration.Tests.TestData;

/// <summary>Builders for scenario route tables (the fake router's (from, to) → leg entries).</summary>
public static class Routes
{
    /// <summary>A leg of <paramref name="hours"/> of driving (12 ticks per hour, 80 km per hour).</summary>
    public static RouteLeg Hours(double hours) =>
        new(DistanceKm: hours * 80, TimeTicks: (int)Math.Round(hours * 12));

    /// <summary>
    /// The legs for sequential shipments P1 → P2, P3 → P4, ... assigned one after another,
    /// each appended at the end: office → P1, each shipment leg, a hop between shipments,
    /// and the final office leg. Every appended shipment re-measures the office leg from its
    /// delivery, so the router is also asked "delivery k → office" for each earlier shipment;
    /// those legs are replaced by the next shipment and get <paramref name="replacedOfficeHours"/>.
    /// </summary>
    public static List<(GeoLocation From, GeoLocation To, RouteLeg Leg)> Sequential(
        double[] shipmentHours,
        double firstHours = 1,
        double hopHours = 1,
        double officeHours = 1,
        double replacedOfficeHours = 3)
    {
        var legs = new List<(GeoLocation, GeoLocation, RouteLeg)> { (TestPoints.Office, TestPoints.P(1), Hours(firstHours)) };
        var count = shipmentHours.Length;

        for (var k = 1; k <= count; k++)
        {
            var pickup = TestPoints.P(2 * k - 1);
            var delivery = TestPoints.P(2 * k);
            legs.Add((pickup, delivery, Hours(shipmentHours[k - 1])));

            if (k < count)
            {
                legs.Add((delivery, TestPoints.P(2 * k + 1), Hours(hopHours)));
                legs.Add((delivery, TestPoints.Office, Hours(replacedOfficeHours)));
            }
            else
            {
                legs.Add((delivery, TestPoints.Office, Hours(officeHours)));
            }
        }

        return legs;
    }

    public static PairLegRoutingService Router(IEnumerable<(GeoLocation From, GeoLocation To, RouteLeg Leg)> legs) => new(legs);
}
