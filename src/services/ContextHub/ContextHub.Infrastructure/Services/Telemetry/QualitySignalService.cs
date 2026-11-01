namespace ContextHub.Infrastructure.Services.Telemetry;

/// <summary>
/// Performs atomic signal-budget and persistence operations without deciding use-case policy.
/// </summary>
/// <param name="qualitySignalRepository">The authoritative package and dedupe boundary.</param>
/// <param name="atomicCacheService">The existing Redis coordination service with no memory fallback.</param>
/// <param name="logger">The metadata-only dependency boundary logger.</param>
public sealed class QualitySignalService(
    IQualitySignalRepository qualitySignalRepository,
    IAtomicCacheService atomicCacheService,
    ILogger<QualitySignalService> logger) : IQualitySignalService
{
    /// <summary>
    /// Increments one authenticated client's fixed-window counter without resetting its original expiry.
    /// </summary>
    /// <param name="clientId">The application identity supplied by the protected use case.</param>
    /// <param name="cancellationToken">Cancels Redis coordination.</param>
    /// <returns>The shared atomic count for the current window.</returns>
    public async Task<long> IncrementBudgetAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Different browsers share the same Redis key; unavailable coordination never falls back to local memory.
            var key = CacheKey.Create("quality-signal-budget", 1, clientId.ToString("N"));

            return await atomicCacheService.IncrementAsync(
                key.Value,
                TimeSpan.FromSeconds(TelemetryConstants.QUALITY_SIGNAL_WINDOW_SECONDS),
                cancellationToken);
        }
        catch (Exception dependencyFailure) when (dependencyFailure is RedisException or RedisTimeoutException)
        {
            // Only known Redis failures become dependency errors; cancellation retains its original path.
            logger.LogWarning(TelemetryLogConstants.QUALITY_DEPENDENCY_UNAVAILABLE, dependencyFailure.GetType().Name);
            throw new DependencyUnavailableException(TelemetryErrorConstants.QUALITY_SIGNAL_DEPENDENCY_UNAVAILABLE, dependencyFailure);
        }
    }

    /// <summary>
    /// Resolves current package metadata before atomically inserting or merging one coded signal.
    /// </summary>
    /// <param name="request">The signal accepted by the Application validator.</param>
    /// <param name="clientId">The authenticated client used for deduplication.</param>
    /// <param name="cancellationToken">Cancels package resolution and persistence.</param>
    /// <returns>True for a new signal, or false for a deduplicated item.</returns>
    public async Task<bool> PersistAsync(
        QualitySignalRequestDto request,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Package lifecycle and step binding use current database authority rather than caller-supplied metadata.
            var packageId = await qualitySignalRepository.ResolvePackageAsync(request, cancellationToken);

            return await qualitySignalRepository.UpsertAsync(request, clientId, packageId, cancellationToken);
        }
        catch (NpgsqlException dependencyFailure) when (dependencyFailure.IsTransient)
        {
            // Schema, programming and cancellation failures are not reclassified as provider unavailability.
            logger.LogWarning(TelemetryLogConstants.QUALITY_DEPENDENCY_UNAVAILABLE, dependencyFailure.GetType().Name);
            throw new DependencyUnavailableException(TelemetryErrorConstants.QUALITY_SIGNAL_DEPENDENCY_UNAVAILABLE, dependencyFailure);
        }
    }
}
