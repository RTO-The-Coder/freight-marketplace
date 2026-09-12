using System.Text.Json;
using System.Text.Json.Serialization;

namespace Freight.Api.Tests.TestSupport;

/// <summary>
/// Base for every controller/middleware/integration test class. Each test class gets its
/// own <see cref="ApiTestFactory"/>, backed by its own freshly-migrated throwaway
/// database, and its own <see cref="HttpClient"/>. DisposeAsync drops that database
/// entirely - no row-level delete/truncate logic, and no data shared with any other test
/// class, test project, or the dev database.
/// </summary>
public abstract class ApiTestBase : IAsyncLifetime
{
    /// <summary>
    /// Mirrors Program.cs's own JsonSerializerOptions (JsonStringEnumConverter) - the
    /// server serializes enums as strings, so client-side (de)serialization in these
    /// tests must use the same converter or every enum-bearing DTO fails to round-trip.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ApiTestFactory Factory { get; } = new();
    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Factory.CreateDatabaseAsync();
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DropDatabaseAsync();
        await Factory.DisposeAsync();
    }
}
