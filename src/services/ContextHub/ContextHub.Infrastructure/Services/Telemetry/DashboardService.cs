namespace ContextHub.Infrastructure.Services.Telemetry;

/// <summary>
/// Exposes dashboard aggregates without giving Application an entity query or context.
/// </summary>
/// <param name="dashboardRepository">The service-owned aggregate reader.</param>
public sealed class DashboardService(IDashboardRepository dashboardRepository) : IDashboardService
{
    /// <summary>
    /// Reads the inclusive UTC range approved by the Application handler.
    /// </summary>
    /// <param name="request">The resolved date range.</param>
    /// <param name="cancellationToken">Cancels database reads.</param>
    /// <returns>The approved aggregate.</returns>
    public Task<DashboardSummaryResponseDto> GetSummaryAsync(DashboardRequestDto request, CancellationToken cancellationToken) =>
        dashboardRepository.GetSummaryAsync(request.From!.Value, request.To!.Value, cancellationToken);
}
