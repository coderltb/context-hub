namespace ContextHub.Infrastructure.DependencyInjection.Integration;

/// <summary>
/// Registers service-owned source and hosted MCP adapters using the existing shared infrastructure.
/// </summary>
public static class IntegrationServiceRegistration
{
    /// <summary>
    /// Registers bounded native HTTP transports, specific repositories and scoped integration services.
    /// </summary>
    /// <param name="services">The Context Hub service collection.</param>
    /// <param name="configuration">Deployment-owned integration settings and private keys.</param>
    /// <returns>The same collection for composition.</returns>
    public static IServiceCollection AddIntegrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services.Any(value => value.ServiceType == typeof(IHostedService) && value.ImplementationType == typeof(ExternalMetadataRefreshService)))
        {
            return services;
        }

        services.AddOptions<GitHubOptions>().Bind(configuration.GetSection(IntegrationInfrastructureConstants.GITHUB_SECTION));
        services.AddOptions<ExternalMcpOptions>().Bind(configuration.GetSection(IntegrationInfrastructureConstants.EXTERNAL_SECTION));
        services.AddMemoryCache();

        // HTTP diagnostics never capture App assertions, installation tokens or upstream protocol bodies.
        services.AddHttpClient<IGitHubSourceClient, GitHubSourceClient>(client =>
            {
                client.BaseAddress = new Uri(IntegrationInfrastructureConstants.GITHUB_API);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(IntegrationInfrastructureConstants.USER_AGENT);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            })
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler {
                AllowAutoRedirect = false,
                UseCookies = false
            })
            .AddHttpResilience()
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(IntegrationInfrastructureConstants.REQUEST_TIMEOUT_SECONDS));

        services.AddTransient<ExternalMcpSafetyHandler>();
        services.AddHttpClient(IntegrationInfrastructureConstants.EXTERNAL_HTTP_CLIENT)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(ExternalEndpointHelper.CreateHandler)
            .AddHttpMessageHandler<ExternalMcpSafetyHandler>()
            .AddHttpResilience()
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(IntegrationInfrastructureConstants.REQUEST_TIMEOUT_SECONDS));

        // Repositories share the one scoped context registered by the service composition root.
        services.TryAddScoped<IAuthoringRepository, AuthoringRepository>();
        services.TryAddScoped<IExternalConnectionRepository, ExternalConnectionRepository>();
        services.TryAddScoped<IRepositoryService, RepositoryService>();
        services.TryAddScoped<IPackageSourceReader, GitHubPackageSourceReader>();
        services.TryAddScoped<IExternalMcpClient, ExternalMcpClient>();
        services.TryAddScoped<IExternalToolRegistry, ExternalToolRegistry>();
        services.TryAddScoped<IExternalMcpService, ExternalMcpService>();
        services.TryAddScoped<IManagedToolService, ManagedToolService>();
        services.AddHostedService<ExternalMetadataRefreshService>();

        return services;
    }
}
