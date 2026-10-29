namespace ContextHub.Authentication.Api.DependencyInjection;

/// <summary>
/// Composes the Authentication host using existing shared boundaries and native protocol services.
/// </summary>
public static class ApiServiceRegistration
{
    /// <summary>
    /// Registers controllers, SSO pages and documentation without opening a database or creating accounts.
    /// </summary>
    /// <param name="services">The host-owned container.</param>
    /// <param name="configuration">Host settings for protocol, email and cache, including external secret overrides.</param>
    /// <param name="environment">The host environment selecting development documentation and protocol metadata.</param>
    /// <returns>The same collection for composition.</returns>
    public static IServiceCollection AddAuthenticationApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Load global mapping profiles and shared Mediator behaviors before feature requests can be dispatched.
        services.AddCoreInfrastructure();
        services.AddApplication();

        // Admin JSON alone uses this boundary; Razor and OIDC retain their native response contracts.
        services.AddApiInfrastructure("/" + ApiRouteConstants.ADMIN_V1, ApiValueConstants.API_VERSION);
        services.AddHttpAudit();
        services.AddAuthenticationInfrastructure(configuration, environment);

        // Register MVC and Razor as separate transports; protocol and HTML responses keep their native contracts.
        services.AddControllers().AddApplicationPart(typeof(ApiServiceRegistration).Assembly);
        ConfigureSsoPages(services);
        services.AddHealthInfrastructure();

        // Compose documentation and exact-origin CORS from the same host/service registrations used at runtime.
        AddDocumentation(services, configuration);
        ConfigureCors(services);
        ConfigureForwardedHeaders(services);

        return services;
    }

    /// <summary>
    /// Maps existing Razor pages to clean URLs while preserving native page-name link generation.
    /// </summary>
    /// <param name="services">The host container registering the native Razor options.</param>
    private static void ConfigureSsoPages(IServiceCollection services)
    {
        services.AddRazorPages(options => options.Conventions.AddFolderRouteModelConvention(
            SsoPageConstants.ACCOUNT_FOLDER, model =>
            {
                if (!SsoPageConstants.ROUTES.TryGetValue(model.ViewEnginePath, out var route))
                {
                    return;
                }

                // Replace the native selector template; adding another route would keep the old public URL alive.
                foreach (var selector in model.Selectors)
                {
                    selector.AttributeRouteModel!.Template = route.TrimStart('/');
                }
            }));
    }

    /// <summary>
    /// Configures the native development document and endpoint-metadata-based Bearer examples in one place.
    /// </summary>
    /// <param name="services">The API container with the registered response boundary.</param>
    /// <param name="configuration">The shared Swagger UI settings bound from the host configuration.</param>
    private static void AddDocumentation(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // A direct native call lets the source generator attach this assembly's XML documentation.
        services.AddOpenApi();

        // Host configuration controls native UI behavior while endpoint-specific fixtures remain explicit overrides.
        services.Configure<SwaggerUIOptions>(configuration.GetSection(ApiCommonOpenApiConstants.SWAGGER_UI_SECTION));
        services.AddOpenApiInfrastructure(registration => registration
            .ScanExamples(typeof(ApiServiceRegistration).Assembly)
            .ApiErrorResponses()
            .Operation<CreateUserRequestDto, ApiResponse<UserResponseDto>>(
                StatusCodes.Status201Created, StatusCodes.Status409Conflict));

        // Attach service title/Bearer metadata to the native document without a second schema or example engine.
        services.Configure<OpenApiOptions>(ApiValueConstants.OPENAPI_DOCUMENT, options =>
        {
            options.AddDocumentTransformer<AuthenticationDocumentTransformer>();
            options.AddOperationTransformer<AuthenticationDocumentTransformer>();
        });
    }

    /// <summary>
    /// Binds exact cross-origin allowlists without changing same-origin Swagger or SSO behavior.
    /// </summary>
    /// <param name="services">The host container supplying native deployment options.</param>
    private static void ConfigureCors(IServiceCollection services)
    {
        // Same-origin Swagger/SSO needs no CORS grant; cross-origin clients need an explicit deployment allowlist.
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<AuthenticationServiceOptions>>((options, configured) =>
            options.AddPolicy(ApiValueConstants.CORS_POLICY, policy =>
            {
                if (configured.Value.CorsOrigins.Length > 0)
                {
                    policy.WithOrigins(configured.Value.CorsOrigins).AllowAnyHeader().AllowAnyMethod();
                }
            }));
    }

    /// <summary>
    /// Trusts the configured immediate proxy while preserving native loopback defaults.
    /// </summary>
    /// <param name="services">The container owning native forwarded-header options.</param>
    private static void ConfigureForwardedHeaders(IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<AuthenticationServiceOptions>>((options, configured) =>
            {
                // Accept scheme and address from one trusted hop; host headers do not override the canonical issuer.
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = ApiValueConstants.FORWARDED_LIMIT;
                foreach (var proxy in configured.Value.KnownProxies)
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }
            });
    }
}
