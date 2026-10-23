namespace ContextHub.Core.Interfaces.Caches;

/// <summary>
/// Defines short-lived coordination primitives, not authoritative business state.
/// </summary>
public interface IAtomicCacheService
{
    /// <summary>
    /// Reserves an absent key with one fixed expiration window.
    /// </summary>
    /// <param name="key">The service-owned coordination key.</param>
    /// <param name="value">The value to reserve, never logged.</param>
    /// <param name="expiration">A strictly positive fixed lifetime.</param>
    /// <param name="cancellationToken">Cancels before dispatch; cannot undo a dispatched Redis command.</param>
    /// <returns>True only when the reservation was created.</returns>
    Task<bool> TrySetIfAbsentAsync(string key, string value, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a counter without extending its original expiration.
    /// </summary>
    /// <param name="key">The service-owned counter key.</param>
    /// <param name="expiration">The lifetime assigned only on first creation.</param>
    /// <param name="cancellationToken">The cancellation token; no automatic retry is performed.</param>
    /// <returns>The new counter value.</returns>
    Task<long> IncrementAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one coordination entry without pattern deletion.
    /// </summary>
    /// <param name="key">The exact service-owned key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task completing after removal.</returns>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
