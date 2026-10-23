namespace ContextHub.Cache.Services;

/// <summary>
/// Adapts native HybridCache without inventing another serializer, lock or retry layer.
/// </summary>
public sealed class HybridCacheService : ICacheService
{
    /// <summary>
    /// Delegates serialization, cache tiers and miss coalescing to the native cache implementation.
    /// </summary>
    private readonly HybridCache _cache;

    /// <summary>
    /// Records cache phases using the non-sensitive namespace rather than raw keys or payloads.
    /// </summary>
    private readonly ILogger<HybridCacheService> _logger;

    /// <summary>
    /// Preserves host read controls while forbidding source reads and writes for cache-only retrieval.
    /// </summary>
    private readonly HybridCacheEntryOptions _readOptions;

    /// <summary>
    /// Creates the cache adapter with native coalescing and a safe operation logger.
    /// </summary>
    /// <param name="cache">The registered HybridCache instance.</param>
    /// <param name="logger">The safe structured cache logger.</param>
    /// <param name="options">The native host defaults, captured without modifying the caller's configuration.</param>
    public HybridCacheService(
        HybridCache cache,
        ILogger<HybridCacheService> logger,
        IOptions<HybridCacheOptions> options)
    {
        _cache = cache;
        _logger = logger;

        // Per-call flags replace native defaults, so preserve the host's bypass choices before adding read-only flags.
        _readOptions = new HybridCacheEntryOptions
        {
            Flags = (options.Value.DefaultEntryOptions?.Flags ?? HybridCacheEntryFlags.None) |
                    HybridCacheEntryFlags.DisableUnderlyingData |
                    HybridCacheEntryFlags.DisableLocalCacheWrite |
                    HybridCacheEntryFlags.DisableDistributedCacheWrite
        };
    }

    /// <summary>
    /// Reads native cache tiers without producing data, filling either tier or extending an entry's lifetime.
    /// </summary>
    /// <typeparam name="T">The same projection type used when the entry was stored.</typeparam>
    /// <param name="key">The validated exact key.</param>
    /// <param name="cancellationToken">Cancels this cache-only operation.</param>
    /// <returns>The existing value or default; native L2 failure-as-miss behavior is retained.</returns>
    public async ValueTask<T?> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        _logger.LogDebug(CacheLogConstants.EntryLogs.STARTED, nameof(GetAsync), key.Namespace);

        // HybridCache requires a callback, but DisableUnderlyingData prevents it from running or caching a miss.
        var value = await _cache.GetOrCreateAsync<T>(
            key.Value, static _ => default, _readOptions, cancellationToken: cancellationToken);

        // Default-valued entries cannot be distinguished from misses here; do not invent hit/miss diagnostics.
        _logger.LogDebug(CacheLogConstants.EntryLogs.RESOLVED, key.Namespace);

        return value;
    }

    /// <summary>
    /// Gets approved data or delegates the source read to the native coalescing cache.
    /// </summary>
    /// <typeparam name="T">The cache projection.</typeparam>
    /// <param name="key">The validated exact key.</param>
    /// <param name="factory">The source-read callback.</param>
    /// <param name="policy">The explicit L1/L2 lifetime.</param>
    /// <param name="cancellationToken">Cancels this caller.</param>
    /// <returns>The cached or source value.</returns>
    public async ValueTask<T> GetOrCreateAsync<T>(
        CacheKey key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(policy);

        _logger.LogDebug(CacheLogConstants.EntryLogs.STARTED, nameof(GetOrCreateAsync), key.Namespace);

        // Native HybridCache coalesces misses within this instance and owns the factory cancellation token.
        var value = await _cache.GetOrCreateAsync(
            key.Value,
            async token =>
            {
                _logger.LogDebug(CacheLogConstants.EntryLogs.SOURCE_READ, key.Namespace);

                return await factory(token);
            },
            policy.ToOptions(),
            tags: policy.Tags,
            cancellationToken: cancellationToken);

        // A resolved value may come from either cache tier or the factory; do not invent hit/miss telemetry.
        _logger.LogDebug(CacheLogConstants.EntryLogs.RESOLVED, key.Namespace);

        return value;
    }

    /// <summary>
    /// Sets one approved entry without swallowing write failures or cancellation.
    /// </summary>
    /// <typeparam name="T">The cache projection.</typeparam>
    /// <param name="key">The exact key.</param>
    /// <param name="value">The approved non-sensitive data.</param>
    /// <param name="policy">The explicit lifetime.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The native cache write.</returns>
    public async ValueTask SetAsync<T>(
        CacheKey key,
        T value,
        CacheEntryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(policy);

        _logger.LogDebug(CacheLogConstants.EntryLogs.STARTED, nameof(SetAsync), key.Namespace);

        await _cache.SetAsync(key.Value, value, policy.ToOptions(), tags: policy.Tags, cancellationToken: cancellationToken);
        _logger.LogDebug(CacheLogConstants.EntryLogs.STORED, key.Namespace);
    }

    /// <summary>
    /// Removes one exact entry and logs only its capability namespace.
    /// </summary>
    /// <param name="key">The exact key.</param>
    /// <param name="cancellationToken">Stops the invalidation.</param>
    /// <returns>The native removal operation.</returns>
    public async ValueTask RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        _logger.LogDebug(CacheLogConstants.EntryLogs.STARTED, nameof(RemoveAsync), key.Namespace);

        // Remove only this key. Other hosts may retain their L1 copy until its configured expiration.
        await _cache.RemoveAsync(key.Value, cancellationToken);
        _logger.LogDebug(CacheLogConstants.EntryLogs.REMOVED, key.Namespace);
    }

    /// <summary>
    /// Delegates tag invalidation to HybridCache without scanning keys or pretending it is a cross-host L1 purge.
    /// </summary>
    /// <param name="tag">The validated capability-owned group.</param>
    /// <param name="cancellationToken">Stops the invalidation.</param>
    /// <returns>The native invalidation operation.</returns>
    public async ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        CacheEntryPolicy.ValidateTag(tag);

        _logger.LogDebug(CacheLogConstants.EntryLogs.STARTED, nameof(RemoveByTagAsync), nameof(HybridCache));

        // The caller owns the commit boundary. Do not cache credentials, lifecycle decisions or Owner tool state.
        await _cache.RemoveByTagAsync(tag, cancellationToken);
        _logger.LogDebug(CacheLogConstants.EntryLogs.TAG_REMOVED);
    }
}
