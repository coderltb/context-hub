namespace ContextHub.Cache.Services;

/// <summary>
/// Performs atomic Redis commands through the host's shared connection without fallback or command retries.
/// </summary>
public sealed class AtomicCacheService : IAtomicCacheService
{
    /// <summary>
    /// Uses the DI-owned multiplexer for every command.
    /// </summary>
    private readonly IConnectionMultiplexer _connection;

    /// <summary>
    /// Records only outcome metadata.
    /// </summary>
    private readonly ILogger<AtomicCacheService> _logger;

    /// <summary>
    /// Uses the same Redis connection as cache and locks.
    /// </summary>
    /// <param name="connection">The DI-owned shared multiplexer.</param>
    /// <param name="logger">The safe metadata logger.</param>
    public AtomicCacheService(IConnectionMultiplexer connection, ILogger<AtomicCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(logger);

        _connection = connection;
        _logger = logger;
    }

    /// <summary>
    /// Reserves an absent key with one fixed expiration window.
    /// </summary>
    /// <param name="key">The service-owned coordination key.</param>
    /// <param name="value">The value to reserve, never logged.</param>
    /// <param name="expiration">A strictly positive fixed lifetime.</param>
    /// <param name="cancellationToken">Cancels before dispatch; cannot undo a dispatched Redis command.</param>
    /// <returns>True only when the reservation was created.</returns>
    public Task<bool> TrySetIfAbsentAsync(string key, string value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(key, expiration);

        // SET NX and its expiry are one server operation; never follow a successful write with a separate TTL command.
        return RunAsync(nameof(TrySetIfAbsentAsync), () => _connection.GetDatabase()
            .StringSetAsync(key, value, expiration, when: When.NotExists), cancellationToken);
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
        Validate(key, expiration);

        return RunAsync(nameof(IncrementAsync), async () =>
        {
            // A Lua script closes the INCR/PEXPIRE race. Existing counters retain their original TTL.
            var milliseconds = Math.Max(1, checked((long)Math.Ceiling(expiration.TotalMilliseconds)));
            var result = await _connection.GetDatabase().ScriptEvaluateAsync(
                CacheConstants.AtomicVariables.INCREMENT_WITH_EXPIRATION,
                [new RedisKey(key)], [(RedisValue)milliseconds]);

            return (long)result;
        }, cancellationToken);
    }

    /// <summary>
    /// Removes one coordination entry without pattern deletion.
    /// </summary>
    /// <param name="key">The exact service-owned key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task completing after removal.</returns>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await RunAsync(nameof(RemoveAsync), () => _connection.GetDatabase().KeyDeleteAsync(key), cancellationToken);
    }

    /// <summary>
    /// Validates key and lifetime before issuing any Redis command.
    /// </summary>
    /// <param name="key">The exact internal key.</param>
    /// <param name="expiration">The requested positive lifetime.</param>
    internal static void Validate(string key, TimeSpan expiration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiration), CacheErrorConstants.AtomicErrors.EXPIRATION_INVALID);
        }
    }

    /// <summary>
    /// Preserves provider failure and cancellation without retrying an ambiguous mutation.
    /// </summary>
    /// <typeparam name="T">The command result type.</typeparam>
    /// <param name="operation">The safe operation name.</param>
    /// <param name="action">The single native command.</param>
    /// <param name="cancellationToken">Cancels the wait; a dispatched mutation may still complete.</param>
    /// <returns>The native command result.</returns>
    private async Task<T> RunAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // WaitAsync cancels waiting, not a Redis command already sent. Retrying an increment could double-count.
        var result = await action().WaitAsync(cancellationToken);
        _logger.LogDebug(CacheLogConstants.AtomicLogs.COMPLETED, operation);

        return result;
    }
}
