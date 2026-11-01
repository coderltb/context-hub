namespace ContextHub.ApiCommon.DependencyInjection;

/// <summary>
/// Groups HTTP-boundary registration while leaving authentication, routing and runtime middleware decisions with the host.
/// </summary>
public static class ApiServiceRegistration
{
    /// <summary>
    /// Registers response adapters for the explicit host-owned API boundary; other protocols retain native responses.
    /// </summary>
    /// <param name="services">The API host's container.</param>
    /// <param name="routePrefix">The complete non-root route prefix owned by this host.</param>
    /// <param name="apiVersion">The public response metadata version.</param>
    /// <returns>The same container for composition at the host.</returns>
    public static IServiceCollection AddApiInfrastructure(
        this IServiceCollection services,
        string routePrefix,
        string apiVersion)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Reject before adding descriptors or callbacks; captured MVC options must use the same boundary as DI factories.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ApiResponseBoundary)))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.BoundaryErrors.ALREADY_REGISTERED);
        }

        // One immutable host-owned boundary is shared by runtime responses and generated documentation.
        var boundary = new ApiResponseBoundary(routePrefix, apiVersion);
        services.AddSingleton(boundary);
        services.AddJsonSerializer();
        services.AddCorrelation();
        services.AddHttpContextAccessor();

        // Read per-request identity/device data through scoped accessors; factories below retain no request-owned state.
        services.TryAddScoped<IClientDeviceContextAccessor, ClientDeviceContextAccessor>();
        services.TryAddSingleton<ApiResponseMetadataFactory>();
        services.TryAddSingleton<ApiErrorResponseFactory>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();

        // The safe handler logs API failures itself; duplicate framework diagnostics could expose raw exceptions.
        services.Configure<ExceptionHandlerOptions>(options => options.SuppressDiagnosticsCallback = context =>
            boundary.Contains(context.HttpContext.Request.Path));

        // Run DTO diagnostics outside result adaptation, then attach the same metadata boundary to completed MVC responses.
        services.Configure<MvcOptions>(options =>
        {
            // MVC sorts the registered TypeFilter wrapper, so its order must also precede native error mapping.
            options.Filters.Add<ApiLoggingFilter>(ApiCommonConstants.API_LOGGING_FILTER_ORDER);
            options.Filters.Add<ApiErrorResponseFilter>(ApiCommonConstants.API_ERROR_FILTER_ORDER);
            options.Filters.Add<ApiResponseMetadataFilter>();
        });

        // MVC initializes its factory during Configure; apply this boundary afterward regardless of host registration order.
        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            // Preserve MVC's original response behavior for Razor/OIDC and other out-of-boundary routes.
            var nativeFactory = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
            {
                if (!boundary.Contains(context.HttpContext.Request.Path))
                {
                    return nativeFactory(context);
                }

                // Binding exceptions can include submitted values. Use a safe message instead of exception text.
                var details = context.ModelState
                    .Where(pair => pair.Value?.Errors.Count > 0)
                    .Select(pair => new ApiErrorDetail(
                        JsonNamingPolicy.CamelCase.ConvertName(pair.Key),
                        pair.Value!.Errors
                            .Select(error => error.Exception is not null || string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? ApiCommonErrorConstants.ApiErrors.INVALID_FIELD
                                : error.ErrorMessage)
                            .Distinct()
                            .ToArray()))
                    .ToArray();

                var factory = context.HttpContext.RequestServices.GetRequiredService<ApiErrorResponseFactory>();
                return new ObjectResult(factory.Create(context.HttpContext, StatusCodes.Status400BadRequest, details))
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { ApiCommonConstants.JSON_CONTENT_TYPE }
                };
            };
        });

        return services;
    }

    /// <summary>
    /// Registers only the clock used by correlation middleware, preserving any explicit host clock.
    /// </summary>
    /// <param name="services">The host container; no API error or MVC adapter is activated.</param>
    /// <returns>The same container for composition.</returns>
    public static IServiceCollection AddCorrelation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Correlation is useful on MCP and scaffold routes too; it must not change their response contracts.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Connects HTTP identity to audit registration without modifying any service DbContext.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">Optional normalized subject mapping.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddHttpAudit(
        this IServiceCollection services,
        Action<CurrentUserOptions>? configure = null)
    {
        // Reuse the selected authenticated principal as the audit actor; feature bodies cannot supply their own actor identifier.
        services.AddCurrentUser(configure);
        services.TryAddScoped<IAuditActorAccessor, HttpAuditActorAccessor>();
        services.AddAudit();

        return services;
    }

    /// <summary>
    /// Registers a scoped reader for the principal normalized by the host.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">Optional subject claim mapping, after token validation.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCurrentUser(
        this IServiceCollection services,
        Action<CurrentUserOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        var options = services.AddOptions<CurrentUserOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.Validate(
            value => !string.IsNullOrWhiteSpace(value.SubjectClaimType),
            CoreErrorConstants.AuthenticationErrors.SUBJECT_CLAIM_REQUIRED).ValidateOnStart();
        services.TryAddScoped<IAuthService, AuthService>();

        return services;
    }

    /// <summary>
    /// Adds only the process self-check and returns the native builder for explicit readiness probes.
    /// </summary>
    /// <param name="services">The host container.</param>
    /// <returns>The health builder; required dependency checks must be tagged ready by the service.</returns>
    public static IHealthChecksBuilder AddHealthInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Dependency outages must not restart a healthy process. Self is intentionally not tagged ready.
        return services.AddHealthChecks().AddCheck(HealthConstants.SELF, () => HealthCheckResult.Healthy(), tags: [HealthConstants.LIVENESS_TAG]);
    }

    /// <summary>
    /// Adds a DI-owned kebab-case transformer for controller/action tokens in the selected MVC host.
    /// </summary>
    /// <param name="builder">The MVC builder selected explicitly by the host.</param>
    /// <returns>The same builder for further native MVC configuration.</returns>
    public static IMvcBuilder AddKebabCaseRouting(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Native options resolve the stateless transformer from this host without creating another provider.
        builder.Services.TryAddSingleton<SlugifyTransformer>();
        builder.Services.AddOptions<MvcOptions>().Configure<SlugifyTransformer>((options, transformer) =>
            options.Conventions.Add(new RouteTokenTransformerConvention(transformer)));

        // Only bracket tokens are transformed. Literal paths and parameter binding stay owned by MVC and attributes.
        return builder;
    }

    /// <summary>
    /// Configures explicit typed fixtures and operation contracts once for a native document.
    /// </summary>
    /// <param name="services">The host container.</param>
    /// <param name="configure">The host's explicit type mappings.</param>
    /// <param name="documentName">The native document name.</param>
    /// <returns>The host container.</returns>
    public static IServiceCollection AddOpenApiInfrastructure(
        this IServiceCollection services,
        Action<OpenApiRegistration> configure,
        string documentName = ApiCommonOpenApiConstants.DEFAULT_DOCUMENT_NAME)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Documentation must use the same boundary as runtime; there is no implicit product-specific prefix.
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(ApiResponseBoundary)))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.BoundaryErrors.NOT_REGISTERED);
        }

        // A second registry would make transformer resolution depend on registration order rather than a single authority.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(OpenApiRegistry)))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.DUPLICATE_MAPPING);
        }

        var registration = new OpenApiRegistration(services);
        configure(registration);

        // Store only a frozen type/status snapshot; providers are activated later through the document's DI services.
        services.AddSingleton(registration.Build());

        // The host calls AddOpenApi directly so its XML documentation generator sees the application assembly.
        services.Configure<OpenApiOptions>(documentName, options => options.AddApiExamples());

        return services;
    }

    /// <summary>
    /// Adds explicit example metadata support without configuring routes, UI or protocol endpoints.
    /// </summary>
    /// <param name="options">The host-owned OpenAPI options.</param>
    /// <returns>The same options for further host configuration.</returns>
    public static OpenApiOptions AddApiExamples(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.AddSchemaTransformer<ApiEnvelopeSchemaTransformer>()
            .AddOperationTransformer<ApiContractOperationTransformer>()
            .AddOperationTransformer<ApiExamplesOperationTransformer>()
            .AddOperationTransformer<ApiValueExamplesOperationTransformer>();
    }
}
