namespace ContextHub.Cache.Services;

/// <summary>
/// Provides explicitly selected process-local coordination; never acts as an automatic Redis fallback.
/// </summary>
public sealed class InMemoryAtomicCacheService : IAtomicCacheService
{
    /// <summary>
    /// Stores immutable snapshots so a cleanup observation cannot delete a newer value.
    /// </summary>
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Schedules occasional cleanup without creating timers or background services.
    /// </summary>
    private long _operations;

    /// <summary>
    /// Controls expiration without depending on wall-clock calls in tests.
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// Records operation metadata without keys or values.
    /// </summary>
    private readonly ILogger<InMemoryAtomicCacheService> _logger;

    /// <summary>
    /// Binds memory coordination to DI-owned time and logging.
    /// </summary>
    /// <param name="clock">The time provider.</param>
    /// <param name="logger">The metadata logger.</param>
    public InMemoryAtomicCacheService(TimeProvider clock, ILogger<InMemoryAtomicCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Reserves an absent key with one fixed expiration window.
    /// </summary>
    /// <param name="key">The service-owned coordination key.</param>
    /// <param name="value">The value to reserve, never logged.</param>
    /// <param name="expiration">A strictly positive fixed lifetime.</param>
    /// <param name="cancellationToken">Cancels before the process-local reservation is changed.</param>
    /// <returns>True only when the reservation was created.</returns>
    public Task<bool> TrySetIfAbsentAsync(string key, string value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        AtomicCacheService.Validate(key, expiration);
        cancellationToken.ThrowIfCancellationRequested();

        SweepExpiredEntries();
        cancellationToken.ThrowIfCancellationRequested();
        var proposed = new Entry(value, _clock.GetUtcNow().Add(expiration));
        var stored = _entries.AddOrUpdate(key, proposed, (_, current) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only an absent or expired snapshot can install this reservation.
            return current.ExpiresAt <= _clock.GetUtcNow() ? proposed : current;
        });
        _logger.LogDebug(CacheLogConstants.AtomicLogs.COMPLETED, nameof(TrySetIfAbsentAsync));

        return Task.FromResult(ReferenceEquals(stored, proposed));
    }

    /// <summary>
    /// Atomically increments a counter without extending its original expiration.
    /// </summary>
    /// <param name="key">The service-owned counter key.</param>
    /// <param name="expiration">The lifetime assigned only on first creation.</param>
    /// <param name="cancellationToken">The cancellation token; no automatic retry is performed.</param>
    /// <returns>The new counter value.</returns>
    public Task<long> IncrementAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        AtomicCacheService.Validate(key, expiration);
        cancellationToken.ThrowIfCancellationRequested();

        SweepExpiredEntries();
        cancellationToken.ThrowIfCancellationRequested();

        // Native update callbacks may replay; they only construct immutable snapshots.
        var stored = _entries.AddOrUpdate(key,
            _ =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return new Entry("1", _clock.GetUtcNow().Add(expiration));
            },
            (_, current) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var now = _clock.GetUtcNow();
                if (current.ExpiresAt <= now)
                {
                    return new Entry("1", now.Add(expiration));
                }

                // Invalid or overflowing counters fail before native replacement and retain their original TTL.
                var count = checked(long.Parse(current.Value, CultureInfo.InvariantCulture) + 1);

                return new Entry(count.ToString(CultureInfo.InvariantCulture), current.ExpiresAt);
            });
        _logger.LogDebug(CacheLogConstants.AtomicLogs.COMPLETED, nameof(IncrementAsync));

        return Task.FromResult(long.Parse(stored.Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Removes one coordination entry without pattern deletion.
    /// </summary>
    /// <param name="key">The exact service-owned key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task completing after removal.</returns>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        SweepExpiredEntries();
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryRemove(key, out _);
        _logger.LogDebug(CacheLogConstants.AtomicLogs.COMPLETED, nameof(RemoveAsync));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reclaims expired snapshots with conditional removal rather than deleting by key alone.
    /// </summary>
    private void SweepExpiredEntries()
    {
        if (Interlocked.Increment(ref _operations) % CacheConstants.AtomicVariables.SWEEP_INTERVAL != 0)
        {
            return;
        }

        var now = _clock.GetUtcNow();
        foreach (var entry in _entries)
        {
            // A writer may have replaced this snapshot after enumeration. Equality-checked removal protects its new TTL.
            if (entry.Value.ExpiresAt <= now)
            {
                _entries.TryRemove(entry);
            }
        }
    }

    /// <summary>
    /// Stores one immutable value and its fixed UTC expiration boundary.
    /// </summary>
    /// <param name="Value">The internal value, never logged.</param>
    /// <param name="ExpiresAt">The fixed expiration instant.</param>
    private sealed record Entry(string Value, DateTimeOffset ExpiresAt);
}
