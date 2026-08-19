using Xunit;

// Every test class in this assembly migrates the same shared database
// (a hardcoded connection string pointing at freight_marketplace) in
// InitializeAsync. xUnit parallelizes across test classes by default, so
// without this the concurrent Database.MigrateAsync() calls race each other
// and fail intermittently with "relation already exists" / "table does not
// exist" (seen in CI) - same issue as Freight.Api.Tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
