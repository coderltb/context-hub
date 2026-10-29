namespace ContextHub.ApiCommon.DependencyInjection;

/// <summary>
/// Composes named and typed HTTP clients with explicit handlers, correlation and safe native resilience.
/// </summary>
public static class HttpClientServiceRegistration
{
    /// <summary>
    /// Registers one named transport with correlation and native resilience, leaving content handling to its caller.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="name">A bounded internal client name, not a consumer-provided URL.</param>
    /// <param name="configure">The service-owned endpoint and transport configuration.</param>
    /// <param name="configureResilience">Optional native timeout and safe-retry tuning.</param>
    /// <returns>The builder for further explicit host configuration.</returns>
    public static IHttpClientBuilder AddHttpClientInfrastructure(
        this IServiceCollection services,
        string name,
        Action<HttpClient> configure,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
    {
        // The main overload validates services/name; the captured callback must be checked before wrapping it.
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddHttpClientInfrastructure(name, (_, client) => configure(client), configureResilience);
    }

    /// <summary>
    /// Registers a named client whose service-owned configuration is resolved from the native factory's provider.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="name">The bounded internal transport name.</param>
    /// <param name="configure">Native provider-aware configuration, including service-owned typed options.</param>
    /// <param name="configureResilience">Optional native timeout and safe-retry tuning.</param>
    /// <returns>The builder for additional handlers or native configuration.</returns>
    public static IHttpClientBuilder AddHttpClientInfrastructure(
        this IServiceCollection services,
        string name,
        Action<IServiceProvider, HttpClient> configure,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddHttpCorrelation();
        services.AddRestClients();

        // Native factory logs include request URIs. Transport metadata logs replace them without exposing paths or credentials.
        // Pooled cookie containers must not share identity between requests made on behalf of different consumers.
        return services.AddHttpClient(name, configure)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false })
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpResilience(configureResilience);
    }

    /// <summary>
    /// Registers a service-owned typed integration using the same explicit named transport pipeline.
    /// </summary>
    /// <typeparam name="TClient">The typed integration contract.</typeparam>
    /// <typeparam name="TImplementation">The implementation accepting a native HttpClient.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="name">The bounded internal transport name.</param>
    /// <param name="configure">The service-owned client configuration.</param>
    /// <param name="configureResilience">Optional native timeout and safe-retry tuning.</param>
    /// <returns>The builder without a competing typed-client service registration.</returns>
    public static IHttpClientBuilder AddHttpClientInfrastructure<TClient, TImplementation>(
        this IServiceCollection services,
        string name,
        Action<HttpClient> configure,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
        where TClient : class
        where TImplementation : class, TClient =>
        services.AddHttpClientInfrastructure(name, configure, configureResilience).AddTypedClient<TClient, TImplementation>();

    /// <summary>
    /// Registers a typed integration with provider-aware options and the same native handler composition.
    /// </summary>
    /// <typeparam name="TClient">The typed integration contract.</typeparam>
    /// <typeparam name="TImplementation">The implementation accepting HttpClient.</typeparam>
    /// <param name="services">The host container.</param>
    /// <param name="name">The bounded internal transport name.</param>
    /// <param name="configure">Service-owned provider-aware client configuration.</param>
    /// <param name="configureResilience">Optional native timeout and safe-retry tuning.</param>
    /// <returns>The builder for explicit handler and native transport configuration.</returns>
    public static IHttpClientBuilder AddHttpClientInfrastructure<TClient, TImplementation>(
        this IServiceCollection services,
        string name,
        Action<IServiceProvider, HttpClient> configure,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
        where TClient : class
        where TImplementation : class, TClient =>
        services.AddHttpClientInfrastructure(name, configure, configureResilience).AddTypedClient<TClient, TImplementation>();

    /// <summary>
    /// Registers the correlation handler for named clients that explicitly add it to their pipeline.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The service collection for explicit host composition.</returns>
    public static IServiceCollection AddHttpCorrelation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddTransient<CorrelationIdHandler>();

        return services;
    }
}
