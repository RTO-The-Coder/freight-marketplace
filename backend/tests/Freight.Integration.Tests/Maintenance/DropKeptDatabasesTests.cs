using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;

namespace Freight.Integration.Tests.Maintenance;

/// <summary>
/// Not a real test - a deliberate cleanup trigger for databases kept by failed tests. It does
/// nothing unless FREIGHT_CLEAN_TEST_DBS=1, because running it alongside other tests would
/// drop their databases mid-run. Run it on its own:
/// <c>$env:FREIGHT_CLEAN_TEST_DBS=1; dotnet test tests/Freight.Integration.Tests --filter Category=Cleanup</c>
/// </summary>
public sealed class DropKeptDatabasesTests(ITestOutputHelper output)
{
    private const string EnableVariable = "FREIGHT_CLEAN_TEST_DBS";

    [Fact]
    [Trait("Category", "Cleanup")]
    public async Task DropKeptDatabases_WhenEnabled_DropsAllIntegrationTestDatabases()
    {
        if (Environment.GetEnvironmentVariable(EnableVariable) != "1")
        {
            output.WriteLine($"Skipped: set {EnableVariable}=1 to drop kept integration-test databases.");
            return;
        }

        var dropped = await IntegrationTestFactory.DropKeptDatabasesAsync();

        output.WriteLine(dropped.Count == 0
            ? "No kept integration-test databases found."
            : $"Dropped {dropped.Count}: {string.Join(", ", dropped)}");
    }
}
