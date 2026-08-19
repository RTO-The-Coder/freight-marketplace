using Xunit;

// Every test class in this assembly runs its own WebApplicationFactory but migrates
// the *same* shared database (ConnectionStrings:FreightDb in appsettings.json) in
// InitializeAsync. xUnit parallelizes across test classes by default, so without this
// the concurrent Database.MigrateAsync() calls race each other and fail intermittently
// with "relation already exists" / "table does not exist" (seen in CI).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
