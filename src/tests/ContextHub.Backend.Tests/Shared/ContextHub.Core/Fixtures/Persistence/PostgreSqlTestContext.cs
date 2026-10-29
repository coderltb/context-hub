namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Fixtures.Persistence;

/// <summary>
/// Creates and resets contexts only for the disposable PostgreSQL test database.
/// </summary>
internal static class PostgreSqlTestContext
{
    /// <summary>
    /// Creates an EF Core context configured for the supplied test-container connection.
    /// </summary>
    /// <param name="connectionString">The fixture-owned database connection string, never included in diagnostic messages.</param>
    /// <returns>A fixture database context owned and disposed by its caller.</returns>
    public static CoreTestDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CoreTestDbContext>()
            .UseNpgsql(connectionString)
            .EnableDetailedErrors()
            .Options;

        return new CoreTestDbContext(options);
    }

    /// <summary>
    /// Recreates the disposable test schema to isolate each persistence test.
    /// </summary>
    /// <param name="connectionString">The fixture-owned database connection string, never included in diagnostic messages.</param>
    /// <returns>A task that completes after the fixture database is reset.</returns>
    public static async Task ResetAsync(string connectionString)
    {
        // The collection passes its disposable container connection; never point this reset at a real database.
        await using var context = Create(connectionString);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
