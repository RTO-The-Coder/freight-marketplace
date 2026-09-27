using Freight.Api.Controllers;
using Freight.Domain.Fleet.Enums;

namespace Freight.Integration.Tests.TestData;

/// <summary>Ready-made POST /trucks bodies. The name is irrelevant to every assertion.</summary>
public static class TestTrucks
{
    /// <summary>Single-driver truck; capacity is far above the small test loads.</summary>
    public static AddTruckBody MediumRefrigerated() =>
        new("Integration Truck", TruckType.Refrigerated, TruckSize.Medium);

    /// <summary>The only size allowed a second driver - for team scenarios.</summary>
    public static AddTruckBody LargeRefrigerated() =>
        new("Integration Team Truck", TruckType.Refrigerated, TruckSize.Large);
}