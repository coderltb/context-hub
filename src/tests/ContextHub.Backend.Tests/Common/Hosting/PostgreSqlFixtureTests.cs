namespace ContextHub.Backend.Tests.Common.Hosting;

/// <summary>
/// Verifies that disposable hosts do not retain connections to an earlier container at the same endpoint.
/// </summary>
public sealed class PostgreSqlFixtureTests
{
    /// <summary>
    /// Creates a fresh schema after Docker reuses the database endpoint of a disposed fixture.
    /// </summary>
    /// <returns>The provider ordering, schema creation and resource ownership assertions.</returns>
    [Fact]
    public async Task ReusedContainerEndpointCreatesSchemaWithFreshHostPool()
    {
        var port = 0;

        for (var iteration = 0; iteration < 2; iteration++)
        {
            await using var database = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("fixture_pool_tests")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithPortBinding(port, 5432)
                .Build();
            await database.StartAsync();
            port = database.GetMappedPublicPort(5432);
            var connectionString = database.GetConnectionString();
            var services = new ServiceCollection();

            // Apply the fixture override after the host's connection-string registration.
            services.AddDbContext<FixtureDbContext>(options => options.UseNpgsql(connectionString));
            services.AddPostgreSqlFixture<FixtureDbContext>(connectionString);

            // Release the host's pool before Docker can reuse this endpoint.
            NpgsqlDataSource dataSource;
            await using (var serviceProvider = services.BuildServiceProvider())
            {
                dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
                await using var scope = serviceProvider.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<FixtureDbContext>();
                await context.Database.ExecuteSqlRawAsync("CREATE TABLE fixture_probe (id integer PRIMARY KEY)");
            }

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await using var connection = await dataSource.OpenConnectionAsync();
            });
        }
    }

    /// <summary>
    /// Uses the native EF provider without introducing application entities into the lifecycle regression.
    /// </summary>
    /// <param name="options">The provider configuration selected by the fixture host.</param>
    private sealed class FixtureDbContext(DbContextOptions<FixtureDbContext> options) : DbContext(options);
}
