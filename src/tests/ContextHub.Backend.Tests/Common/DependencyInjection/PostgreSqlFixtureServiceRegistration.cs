namespace ContextHub.Backend.Tests.Common.DependencyInjection;

/// <summary>
/// Gives disposable test hosts ownership of their PostgreSQL connection pools.
/// </summary>
public static class PostgreSqlFixtureServiceRegistration
{
    /// <summary>
    /// Uses the fixture database while preserving the context's existing provider configuration and interceptors.
    /// </summary>
    /// <typeparam name="TContext">The context already registered by the tested host.</typeparam>
    /// <param name="services">The fixture host's service collection.</param>
    /// <param name="connectionString">The connection to the fixture-owned PostgreSQL container.</param>
    /// <returns>The same collection for further fixture registration.</returns>
    public static IServiceCollection AddPostgreSqlFixture<TContext>(
        this IServiceCollection services,
        string connectionString)
        where TContext : DbContext
    {
        // The host must dispose its pool before Docker reuses the container's endpoint.
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContext<TContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        return services;
    }
}
