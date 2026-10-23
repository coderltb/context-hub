namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.Fixtures;

/// <summary>
/// Owns a disposable PostgreSQL container used by the persistence integration tests.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    /// <summary>
    /// Owns the isolated PostgreSQL container and its test-only credentials.
    /// </summary>
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("context_hub_core_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>
    /// Gets the connection string for the isolated test container.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Starts the disposable PostgreSQL container before the collection's tests run.
    /// </summary>
    /// <returns>A task that completes after the fixture has initialized its test dependencies.</returns>
    public Task InitializeAsync() => _container.StartAsync();

    /// <summary>
    /// Disposes the PostgreSQL container after all tests in the collection finish.
    /// </summary>
    /// <returns>A task that completes after fixture-owned resources have been released.</returns>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
