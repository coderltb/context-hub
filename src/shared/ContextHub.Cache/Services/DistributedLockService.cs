namespace ContextHub.Cache.Services;

/// <summary>
/// Uses the native Redis lock handle for auto-renewal and release, not a custom lock engine.
/// </summary>
public sealed class DistributedLockService : IDistributedLockService
{
    /// <summary>
    /// Shares the host's existing Redis connection.
    /// </summary>
    private readonly IConnectionMultiplexer _connection;

    /// <summary>
    /// Records acquisition outcomes without logging lock keys.
    /// </summary>
    private readonly ILogger<DistributedLockService> _logger;

    /// <summary>
    /// Binds locking to the shared multiplexer.
    /// </summary>
    /// <param name="connection">The DI-owned Redis connection.</param>
    /// <param name="logger">The safe metadata logger.</param>
    public DistributedLockService(
        IConnectionMultiplexer connection,
        ILogger<DistributedLockService> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(logger);

        _connection = connection;
        _logger = logger;
    }

    /// <summary>
    /// Acquires an auto-renewed lease within the native validity budget.
    /// </summary>
    /// <param name="key">The exact internal lock key.</param>
    /// <param name="leaseDuration">The positive provider-supported renewal lease.</param>
    /// <param name="cancellationToken">Cancels acquisition, preserving cancellation exceptions.</param>
    /// <returns>The owned lease, or null when native acquisition does not succeed in time.</returns>
    public async Task<IDistributedLockLease?> TryAcquireAsync(
        string key,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        AtomicCacheService.Validate(key, leaseDuration);
        cancellationToken.ThrowIfCancellationRequested();

        // Validate lease configuration outside the infrastructure catch, so caller mistakes stay argument errors.
        var distributedLock = new RedisDistributedLock(key, _connection.GetDatabase(), options => options.Expiry(leaseDuration));

        try
        {
            // Null can mean contention or exhaustion of the native validity budget; do not reinterpret it as an outage.
            var handle = await distributedLock.TryAcquireAsync(TimeSpan.Zero, cancellationToken);
            _logger.LogDebug(CacheLogConstants.AtomicLogs.LOCK_COMPLETED, handle is not null);

            return handle is null ? null : new DistributedLockLease(handle);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            // Translate only provider outages. Configuration, programming errors and cancellation retain their native types.
            _logger.LogWarning(CacheLogConstants.AtomicLogs.FAILED, nameof(TryAcquireAsync), exception.GetType().Name);

            throw new DistributedLockUnavailableException(exception);
        }
    }
}
