namespace ContextHub.Cache.Interfaces;

/// <summary>
/// Caches explicitly approved non-sensitive data; authority checks remain outside cache hits.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Reads existing cache data without invoking a source factory, writing a placeholder or renewing expiration.
    /// </summary>
    /// <typeparam name="T">The same serializable projection type used to store the entry.</typeparam>
    /// <param name="key">The exact capability-owned key.</param>
    /// <param name="cancellationToken">Cancels this cache read.</param>
    /// <returns>The cached value, or default when absent; a native secondary-store failure may also behave as a miss.</returns>
    ValueTask<T?> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves or produces data with HybridCache's per-instance request coalescing.
    /// </summary>
    /// <typeparam name="T">The serializable cache projection.</typeparam>
    /// <param name="key">The capability-owned key.</param>
    /// <param name="factory">The source read; never close over scoped dependencies in a singleton service.</param>
    /// <param name="policy">The explicit expiration policy.</param>
    /// <param name="cancellationToken">Cancels this caller; the factory receives the combined caller token.</param>
    /// <returns>The cached or freshly read projection.</returns>
    ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory,
        CacheEntryPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores explicitly approved data without changing the database authority.
    /// </summary>
    /// <typeparam name="T">The serializable cache projection.</typeparam>
    /// <param name="key">The exact entry key.</param>
    /// <param name="value">The approved non-sensitive data.</param>
    /// <param name="policy">The explicit expiration policy.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The cache write operation.</returns>
    ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an exact key from this instance's L1 and the configured L2; other hosts' L1 may live until expiry.
    /// </summary>
    /// <param name="key">The exact entry key; no wildcard scan is supported.</param>
    /// <param name="cancellationToken">Stops the invalidation.</param>
    /// <returns>The invalidation operation.</returns>
    ValueTask RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logically invalidates a trusted tag after the owning use case commits; remote L1 entries may persist until expiry.
    /// </summary>
    /// <param name="tag">An exact tag without whitespace or wildcards.</param>
    /// <param name="cancellationToken">Stops the native invalidation.</param>
    /// <returns>The invalidation operation.</returns>
    ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
