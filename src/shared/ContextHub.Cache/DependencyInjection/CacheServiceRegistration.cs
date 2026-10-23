namespace ContextHub.Cache.DependencyInjection;

/// <summary>
/// Composes shared caching and explicit coordination using one host-owned Redis connection pool.
/// </summary>
public static class CacheServiceRegistration
{
    /// <summary>
    /// Registers HybridCache, atomic operations and distributed locking against the same DI-owned Redis multiplexer.
    /// </summary>
    /// <param name="services">The host container owning Redis lifetime.</param>
    /// <param name="configure">The single host-owned Redis configuration callback; credentials are never logged.</param>
    /// <returns>The same collection for further explicit composition.</returns>
    public static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        Action<RedisCacheOptions> configure)
    {
        // The cache entry point validates inputs before mutation, captures configuration once and owns the shared pool.
        services.AddRedisCache(configure);
        services.AddRedisCoordination();

        return services;
    }

    /// <summary>
    /// Registers HybridCache and the capability-facing cache abstraction, with bounded native defaults.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">Optional native cache settings; no network connection is opened during registration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCache(
        this IServiceCollection services,
        Action<HybridCacheOptions>? configure = null)
    {
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = CacheConstants.HybridCacheVariables.MAX_PAYLOAD_BYTES;
            options.MaximumKeyLength = CacheConstants.HybridCacheVariables.MAX_KEY_LENGTH;
            configure?.Invoke(options);
        });
        services.TryAddSingleton<ICacheService, HybridCacheService>();

        return services;
    }

    /// <summary>
    /// Registers Redis as HybridCache's shared secondary store, using host-owned credential configuration.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">The Redis settings supplied by the host; never logged by this registration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        Action<RedisCacheOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Capture host configuration once. Pool creation remains lazy and all Redis capabilities reuse that pool.
        var connectionOptions = new RedisCacheOptions();
        configure(connectionOptions);
        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
            connectionOptions.ConnectionMultiplexerFactory is not null
                ? connectionOptions.ConnectionMultiplexerFactory().GetAwaiter().GetResult()
                : ConnectionMultiplexer.Connect(connectionOptions.ConfigurationOptions ??
                    ConfigurationOptions.Parse(connectionOptions.Configuration ??
                        throw new InvalidOperationException(CacheErrorConstants.AtomicErrors.CONFIGURATION_REQUIRED))));

        services.AddStackExchangeRedisCache(options =>
        {
            options.InstanceName = connectionOptions.InstanceName;
            options.ProfilingSession = connectionOptions.ProfilingSession;
        });
        services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>((options, connection) =>
            options.ConnectionMultiplexerFactory = () => Task.FromResult(connection));

        // The native cache closes its connection only on shutdown; do not enable its force-reconnect AppContext switch.
        return services.AddCache();
    }

    /// <summary>
    /// Registers process-local atomic state; this mode does not provide a distributed lock.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddInMemoryAtomicCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        // Explicit mode selection replaces earlier stores without changing independently configured locks.
        services.RemoveAll<IAtomicCacheService>();
        services.AddSingleton<IAtomicCacheService, InMemoryAtomicCacheService>();

        return services;
    }

    /// <summary>
    /// Registers Redis atomic cache and locks against the previously configured multiplexer.
    /// </summary>
    /// <param name="services">The host service collection, including IConnectionMultiplexer.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRedisCoordination(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Changing atomic mode must not remove a separately configured distributed-lock capability.
        services.RemoveAll<IAtomicCacheService>();
        services.AddSingleton<IAtomicCacheService, AtomicCacheService>();
        services.TryAddSingleton<IDistributedLockService, DistributedLockService>();

        return services;
    }
}
