namespace ContextHub.Infrastructure.Interfaces.Telemetry;

/// <summary>
/// Defines the feature-specific persistence boundary owned by Infrastructure.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="from">The approved from input.</param>
    /// <param name="to">The approved to input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<DashboardSummaryResponseDto> GetSummaryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
