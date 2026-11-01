namespace ContextHub.Infrastructure.DependencyInjection;

/// <summary>
/// Composes the service-owned database, local inference and integration adapters without provisioning schema or content.
/// </summary>
public static class InfrastructureServiceRegistration
{
    /// <summary>
    /// Registers one persistence scope and reuses the shared cache, transaction and HTTP boundaries.
    /// </summary>
    /// <param name="services">The Context Hub host container.</param>
    /// <param name="configuration">Host-owned deployment configuration.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextHubInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddCaching(services, configuration);
        AddProtectionAndEmbedding(services, configuration);
        AddFeatureServices(services, configuration);

        // Connectivity is readiness only; schema parity is verified by DB-first integration tests.
        services.AddHealthChecks().AddDbContextCheck<ContextHubDbContext>(
            InfrastructureConstants.DATABASE_HEALTH_CHECK,
            tags: [InfrastructureConstants.READINESS_TAG]);
        return services;
    }

    /// <summary>
    /// Composes one audited context and native connection pool without provisioning schema.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">The externally supplied database connection.</param>
    private static void AddPersistence(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(configuration.GetConnectionString(InfrastructureConstants.DATABASE_CONNECTION) ?? "");
            builder.UseVector();
            return builder.Build();
        });
        services.AddAuditedDbContext<ContextHubDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), postgres => postgres.UseVector()));
        services.AddPersistence<ContextHubDbContext>();
        services.AddDapper();
    }

    /// <summary>
    /// Reuses one lazy Redis pool for cache and strict coordination.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">Native cache and Redis settings.</param>
    private static void AddCaching(
        IServiceCollection services,
        IConfiguration configuration)
    {

        // A single lazy Redis pool backs cache payloads and strict distributed coordination.
        services.AddRedisConnection(configuration.GetSection(InfrastructureConstants.REDIS_SETTINGS));
        services.AddRedisCache(options => options.InstanceName = InfrastructureConstants.CACHE_PREFIX);
        services.AddRedisCoordination();
        services.Configure<HybridCacheOptions>(configuration.GetSection(InfrastructureConstants.CACHE_SETTINGS));
        services.AddMemoryCache();
    }

    /// <summary>
    /// Registers cursor protection and optional local inference with their existing lifetimes.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">Deployment key-ring and model settings.</param>
    private static void AddProtectionAndEmbedding(
        IServiceCollection services,
        IConfiguration configuration)
    {

        var protection = services.AddDataProtection().SetApplicationName(InfrastructureConstants.DATA_PROTECTION_APPLICATION);
        var keyringPath = configuration[InfrastructureConstants.KEY_RING_PATH];
        if (!string.IsNullOrWhiteSpace(keyringPath))
        {
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyringPath));
        }
        services.Configure<E5EmbeddingOptions>(configuration.GetSection(InfrastructureConstants.EMBEDDING_SETTINGS));
        services.AddSingleton<IEmbeddingService, E5EmbeddingService>();
    }

    /// <summary>
    /// Registers the service-owned ports while reusing Shared repository and transaction boundaries.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">Optional integration settings.</param>
    private static void AddFeatureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<IPackageVersionRepository, PackageVersionRepository>();
        services.AddSingleton<CatalogContentValidator>();
        services.AddScoped<ICatalogSearchService, CatalogSearchService>();
        services.AddScoped<ICatalogResolverService, CatalogResolverService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddIntegrationInfrastructure(configuration);
        services.AddTelemetryFeatureServices();
        services.AddScoped<IMcpPromptRepository, McpPromptRepository>();
        services.AddScoped<IMcpPromptService, McpPromptService>();

    }
}
