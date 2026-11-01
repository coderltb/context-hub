namespace ContextHub.Infrastructure.Services.Integration;

/// <summary>
/// Refreshes rebuildable hosted metadata inside a fresh scoped persistence boundary each iteration.
/// </summary>
/// <param name="scopeFactory">The native scoped-service factory.</param>
/// <param name="logger">The metadata-only refresh logger.</param>
public sealed class ExternalMetadataRefreshService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExternalMetadataRefreshService> logger)
    : BackgroundService
{
    /// <summary>
    /// Refreshes on the configured native interval without creating a scheduler or holding a DbContext in a singleton.
    /// </summary>
    /// <param name="stoppingToken">Cancels scope work and the native timer during host shutdown.</param>
    /// <returns>The lifetime of the hosted worker.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(IntegrationInfrastructureConstants.REFRESH_MINUTES));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IExternalToolRegistry>().RefreshAsync(stoppingToken);
            }
            catch (DependencyUnavailableException exception)
            {
                // Only classified dependency failures leave the host running until the next refresh tick.
                logger.LogWarning(IntegrationLogConstants.REFRESH_FAILED, exception.GetType().Name);
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                return;
            }
        }
    }
}
