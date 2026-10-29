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
    /// Registers one lazy Redis pool from structured host options without changing cache or coordination mode.
    /// </summary>
    /// <param name="services">The host container owning the connection lifetime.</param>
    /// <param name="section">The host-selected RedisSettings section; secrets stay in external configuration.</param>
    /// <returns>The same collection for explicit cache or coordination composition.</returns>
    public static IServiceCollection AddRedisConnection(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        // Preserve an existing host-owned pool and its configuration; repeated composition must not add endpoints or pools.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer)))
        {
            return services;
        }

        // Bind structured deployment settings once and translate them into native connection options without logging credentials.
        services.AddOptions<RedisOptions>().Bind(section);
        services.AddOptions<ConfigurationOptions>().Configure<IOptions<RedisOptions>>((options, configured) =>
        {
            var redis = configured.Value;
            options.EndPoints.Add(redis.Host, redis.Port);
            options.Password = redis.Password;
            options.Ssl = redis.Ssl;
            options.DefaultDatabase = redis.DbNumber;

            // Omitted optional fields retain native client semantics instead of inheriting an application's retry policy.
            options.ConnectRetry = redis.ConnectRetry ?? options.ConnectRetry;
            options.ConnectTimeout = redis.ConnectTimeout ?? options.ConnectTimeout;
            options.SyncTimeout = redis.SyncTimeout ?? options.SyncTimeout;
            options.AsyncTimeout = redis.AsyncTimeout ?? options.AsyncTimeout;
            options.AbortOnConnectFail = redis.AbortOnConnectFail ?? options.AbortOnConnectFail;
        });

        // Open the shared pool only when a registered capability resolves it; DI owns its eventual disposal.
        services.TryAddSingleton<IConnectionMultiplexer>(provider => ConnectionMultiplexer.Connect(
            provider.GetRequiredService<IOptions<ConfigurationOptions>>().Value));

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
        // Bound payload/key sizes before applying the host callback, then fill only the cache lifetimes the host left unspecified.
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = CacheConstants.HybridCacheVariables.MAX_PAYLOAD_BYTES;
            options.MaximumKeyLength = CacheConstants.HybridCacheVariables.MAX_KEY_LENGTH;
            configure?.Invoke(options);

            // Fill only omitted lifetimes after the host callback; preserve flags and repeated-registration overrides.
            var defaults = options.DefaultEntryOptions;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = defaults?.Expiration ?? TimeSpan.FromSeconds(CacheConstants.HybridCacheVariables.DEFAULT_EXPIRATION_SECONDS),
                LocalCacheExpiration = defaults?.LocalCacheExpiration ?? TimeSpan.FromSeconds(CacheConstants.HybridCacheVariables.DEFAULT_LOCAL_EXPIRATION_SECONDS),
                Flags = defaults?.Flags
            };
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
