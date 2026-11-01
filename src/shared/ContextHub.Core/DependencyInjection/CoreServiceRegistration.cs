namespace ContextHub.Core.DependencyInjection;

/// <summary>
/// Composes reusable messaging, JSON, mapping and HTTP services without configuring business policies or persistence.
/// </summary>
public static class CoreServiceRegistration
{
    /// <summary>
    /// Registers shared transport dependencies and authorization before validation, without adding business handlers or policies.
    /// </summary>
    /// <param name="services">The host container owning shared services.</param>
    /// <returns>The same collection for service-owned configuration.</returns>
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
    {
        // Configure loaded profiles before any consumer can map; transport registration never scans again.
        services.AddMapping();
        services.AddRestClients();

        // Authorization is outermost so rejected requests cannot run validators or business work.
        services.AddAuthorizationBehavior();
        services.AddValidationBehavior();

        return services;
    }

    /// <summary>
    /// Registers neutral authorization; each service must register policies for its protected request types.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same collection; the host explicitly orders its pipeline registrations.</returns>
    public static IServiceCollection AddAuthorizationBehavior(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>)));

        return services;
    }

    /// <summary>
    /// Registers the FluentValidation Mediator pipeline behavior.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <returns>The same service collection for registration chaining.</returns>
    public static IServiceCollection AddValidationBehavior(this IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));

        return services;
    }

    /// <summary>
    /// Shares Newtonsoft conventions across REST clients and application consumers using constructor injection.
    /// </summary>
    /// <param name="services">The owning host container.</param>
    /// <returns>The collection for continued composition.</returns>
    public static IServiceCollection AddJsonSerializer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Share immutable JSON settings; the serializer implementation creates operation-local native state for concurrent callers.
        services.TryAddSingleton<IJsonSerializerService, JsonSerializerService>();

        return services;
    }

    /// <summary>
    /// Applies mapping files from loaded assemblies to the process-wide configuration and compiles them at startup.
    /// </summary>
    /// <param name="services">The composition root configuring mappings once before consumers run.</param>
    /// <returns>The same collection; no configuration or mapper is registered in DI.</returns>
    public static IServiceCollection AddMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scan loaded, parameterless profiles before first use; never change global mappings during requests.
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(AppDomain.CurrentDomain.GetAssemblies());
        config.Compile();

        return services;
    }

    /// <summary>
    /// Registers shared JSON and REST services without reconfiguring startup mappings.
    /// </summary>
    /// <param name="services">The owning container.</param>
    /// <returns>The collection for continued composition.</returns>
    public static IServiceCollection AddRestClients(this IServiceCollection services)
    {
        // JSON registration validates the container. Standalone composition calls AddMapping once separately.
        services.AddJsonSerializer();

        services.AddHttpClient();
        services.TryAddTransient<IRestClientMultipleService, RestClientMultipleService>();
        services.TryAddTransient<IThirdPartyApiService, ThirdPartyApiService>();

        return services;
    }

    /// <summary>
    /// Attaches one native resilience pipeline to the selected client, never to all clients globally.
    /// </summary>
    /// <param name="builder">The explicitly selected named or typed client.</param>
    /// <param name="configure">Optional service-owned timeout and safe-retry tuning.</param>
    /// <returns>The same builder for host composition.</returns>
    public static IHttpClientBuilder AddHttpResilience(
        this IHttpClientBuilder builder,
        Action<HttpStandardResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Streaming callers own body timeouts. Buffered SDK clients configure a finite HttpClient timeout after this registration.
        builder.ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);

        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(CoreConstants.HttpVariables.TOTAL_TIMEOUT_SECONDS);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(CoreConstants.HttpVariables.ATTEMPT_TIMEOUT_SECONDS);
            options.Retry.MaxRetryAttempts = CoreConstants.HttpVariables.MAX_RETRY_ATTEMPTS;
            configure?.Invoke(options);

            // Apply this guard last so custom tuning cannot replay mutations, including an MCP POST.
            options.Retry.DisableForUnsafeHttpMethods();
        });

        return builder;
    }

    /// <summary>
    /// Opts a host into the shared SendGrid adapter without enabling mail for every Core consumer.
    /// </summary>
    /// <param name="services">The host-owned container.</param>
    /// <param name="section">The host-selected email section, including external credentials.</param>
    /// <returns>The same collection for composition.</returns>
    public static IServiceCollection AddSendGridEmail(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        // Bind provider credentials from the caller-selected section; feature content and secret sourcing stay outside shared transport registration.
        services.AddOptions<SendGridOptions>().Bind(section);

        services.TryAddScoped<IEmailService, SendGridEmailService>();

        // Preserve explicit SDK overrides and avoid adding another handler pipeline on repeated registration.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ISendGridClient)))
        {
            return services;
        }

        services.AddHttpClient(CoreConstants.EmailVariables.HTTP_CLIENT_NAME)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false })
            .AddHttpResilience(options =>
            {
                options.CircuitBreaker.MinimumThroughput = CoreConstants.EmailVariables.MINIMUM_THROUGHPUT;
                options.CircuitBreaker.FailureRatio = CoreConstants.EmailVariables.FAILURE_RATIO;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(CoreConstants.EmailVariables.SAMPLING_SECONDS);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(CoreConstants.EmailVariables.BREAK_SECONDS);
                section.GetSection(CoreConstants.EmailVariables.RESILIENCE_SECTION).Bind(options);
                // The unsafe-method exclusion is applied after this callback, so mail POST is never retried.
            })
            .ConfigureHttpClient((provider, client) =>
            {
                // The SDK buffers the body after handlers return. This outer timeout also bounds that native buffering.
                client.Timeout = provider.GetRequiredService<IOptions<SendGridOptions>>().Value.RequestTimeout;
            });

        services.TryAddTransient<ISendGridClient>(provider => new SendGridClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(CoreConstants.EmailVariables.HTTP_CLIENT_NAME),
            new SendGridClientOptions
            {
                ApiKey = provider.GetRequiredService<IOptions<SendGridOptions>>().Value.ApiKey,
                HttpErrorAsException = false
            }));

        return services;
    }

    /// <summary>
    /// Requires a section and schedules native validation before the host starts accepting requests.
    /// </summary>
    /// <typeparam name="TOptions">The service-owned settings type.</typeparam>
    /// <param name="services">The host container.</param>
    /// <param name="configuration">The configuration assembled by the host, including environment overrides.</param>
    /// <param name="sectionName">The required internal configuration section.</param>
    /// <returns>The native builder for explicit caller-owned validation rules.</returns>
    public static OptionsBuilder<TOptions> AddConfiguredOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        // Reject missing configuration with safe service-owned messages that never contain option values.
        var section = configuration.GetRequiredSection(sectionName);

        return services.AddOptions<TOptions>().Bind(section).ValidateOnStart();
    }
}
