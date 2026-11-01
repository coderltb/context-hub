namespace ContextHub.Application.Interfaces.Telemetry;

/// <summary>
/// Defines an Application-owned port for privacy-safe feature operations.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Aggregates the applied inclusive UTC range.
    /// </summary>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task<DashboardSummaryResponseDto> GetSummaryAsync(DashboardRequestDto request, CancellationToken cancellationToken);
}
