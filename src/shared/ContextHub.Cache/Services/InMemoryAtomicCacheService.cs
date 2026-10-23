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
    /// Bounds synchronization memory independently of the number of keys.
    /// </summary>
    private readonly object[] _stripes = Enumerable.Range(0, CacheConstants.AtomicVariables.STRIPE_COUNT).Select(_ => new object()).ToArray();

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
        lock (GetStripe(key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveExpiredEntry(key);
            var created = _entries.TryAdd(key, new Entry(value, _clock.GetUtcNow().Add(expiration)));
            _logger.LogDebug(CacheLogConstants.AtomicLogs.COMPLETED, nameof(TrySetIfAbsentAsync));

            return Task.FromResult(created);
        }
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
        lock (GetStripe(key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveExpiredEntry(key);

            // Match Redis fixed-window semantics and reject nonnumeric or overflowing counters without changing state.
            var exists = _entries.TryGetValue(key, out var previous);
            var count = exists ? checked(long.Parse(previous!.Value, CultureInfo.InvariantCulture) + 1) : 1;
            _entries[key] = new Entry(count.ToString(CultureInfo.InvariantCulture),
                exists ? previous!.ExpiresAt : _clock.GetUtcNow().Add(expiration));
            _logger.LogDebug(CacheLogConstants.AtomicLogs.COMPLETED, nameof(IncrementAsync));

            return Task.FromResult(count);
        }
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
        lock (GetStripe(key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries.TryRemove(key, out _);
            _logger.LogDebug(CacheLogConstants.AtomicLogs.COMPLETED, nameof(RemoveAsync));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves a stable, bounded synchronization stripe for a key.
    /// </summary>
    /// <param name="key">The validated key.</param>
    /// <returns>The process-local synchronization object.</returns>
    private object GetStripe(string key) =>
        _stripes[(uint)StringComparer.Ordinal.GetHashCode(key) % (uint)_stripes.Length];

    /// <summary>
    /// Deletes only the expired snapshot that was observed, not a concurrently replaced entry.
    /// </summary>
    /// <param name="key">The key being inspected under its stripe.</param>
    private void RemoveExpiredEntry(string key)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt <= _clock.GetUtcNow())
        {
            _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
        }
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
