namespace ContextHub.Api.DependencyInjection;

/// <summary>
/// Composes the HTTP and MCP host from existing shared boundaries and service-owned adapters.
/// </summary>
public static class ApiServiceRegistration
{
    /// <summary>
    /// Registers feature dispatch, persistence, native authentication and development documentation.
    /// </summary>
    /// <param name="services">The host container.</param>
    /// <param name="configuration">The host's native configuration providers.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextHubApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Load global mappings and shared Mediator behaviors before feature requests can be dispatched.
        services.AddCoreInfrastructure();
        services.AddContextHubApplication();

        // Only Admin REST uses the shared envelope; MCP retains its native protocol contract.
        services.AddApiInfrastructure(HubConstants.ADMIN_PATH, ApiDocumentationConstants.API_VERSION);
        services.AddHttpAudit();
        services.AddContextHubInfrastructure(configuration);
        services.AddContextHubAuthentication(configuration);

        // Register both transports without scanning additional public MCP tools.
        services.AddControllers().AddApplicationPart(typeof(ApiServiceRegistration).Assembly);
        services.AddContextHubMcp(configuration);
        services.AddHealthInfrastructure();

        // Configure documentation, browser origins and independent traffic budgets at the host boundary.
        AddDocumentation(services, configuration);
        ConfigureCors(services);
        AddRateLimits(services, configuration);

        return services;
    }

    /// <summary>
    /// Reuses shared documentation settings while keeping native XML generation in this host.
    /// </summary>
    /// <param name="services">The host service container.</param>
    /// <param name="configuration">Native Swagger UI settings.</param>
    private static void AddDocumentation(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Keep the native call direct so XML documentation is attached by the OpenAPI source generator.
        services.AddOpenApi();
        services.AddOpenApiInfrastructure(registration => registration.ApiErrorResponses());
        services.Configure<SwaggerUIOptions>(configuration.GetSection(ApiCommonOpenApiConstants.SWAGGER_UI_SECTION));
        services.Configure<OpenApiOptions>(
            ApiDocumentationConstants.DOCUMENT_NAME,
            options =>
            {
                options.AddDocumentTransformer<ContextHubDocumentTransformer>();
                options.AddOperationTransformer<ContextHubDocumentTransformer>();
            });
    }

    /// <summary>
    /// Limits browser access to the exact origins configured for this resource server.
    /// </summary>
    /// <param name="services">The host service container.</param>
    private static void ConfigureCors(IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<ResourceServerOptions>>((options, configured) =>
            options.AddPolicy(
                HubConstants.CORS_POLICY,
                policy =>
                {
                    if (configured.Value.AllowedOrigins.Length != 0)
                    {
                        policy.WithOrigins(configured.Value.AllowedOrigins).AllowAnyMethod().AllowAnyHeader();
                    }
                }));
    }

    /// <summary>
    /// Registers two native fixed-window limiters so unauthenticated ingress is bounded before introspection.
    /// </summary>
    /// <param name="services">The host container owning limiter disposal.</param>
    /// <param name="configuration">The non-secret traffic limits.</param>
    private static void AddRateLimits(
        IServiceCollection services,
        IConfiguration configuration)
    {
        AddFixedWindowLimiter(
            services,
            configuration,
            HubConstants.INGRESS_LIMITER,
            HubConstants.IP_RATE_LIMIT_CONFIGURATION_KEY,
            HubConstants.DEFAULT_IP_REQUESTS_PER_MINUTE);

        AddFixedWindowLimiter(
            services,
            configuration,
            HubConstants.CLIENT_LIMITER,
            HubConstants.CLIENT_RATE_LIMIT_CONFIGURATION_KEY,
            HubConstants.DEFAULT_CLIENT_REQUESTS_PER_MINUTE);
    }

    /// <summary>
    /// Shares limiter configuration while keeping ingress and client partition state independent.
    /// </summary>
    /// <param name="services">The container owning the limiter lifetime.</param>
    /// <param name="configuration">The native traffic-limit settings.</param>
    /// <param name="serviceKey">The keyed limiter identity used by middleware.</param>
    /// <param name="configurationKey">The permit-limit setting.</param>
    /// <param name="defaultPermitLimit">The existing allowance when the setting is absent.</param>
    private static void AddFixedWindowLimiter(
        IServiceCollection services,
        IConfiguration configuration,
        string serviceKey,
        string configurationKey,
        int defaultPermitLimit)
    {
        // Resolve each keyed singleton independently so traffic cannot consume another boundary's allowance.
        services.AddKeyedSingleton<PartitionedRateLimiter<string>>(
            serviceKey,
            (_, _) => PartitionedRateLimiter.Create<string, string>(identity =>
                RateLimitPartition.GetFixedWindowLimiter(
                    identity,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = configuration.GetValue(configurationKey, defaultPermitLimit),
                        Window = TimeSpan.FromSeconds(HubConstants.RATE_LIMIT_WINDOW_SECONDS),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })));
    }
}
