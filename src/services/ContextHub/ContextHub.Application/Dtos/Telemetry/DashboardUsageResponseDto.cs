namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains privacy-safe dashboard aggregates.
/// </summary>
public sealed record DashboardUsageResponseDto
{
    /// <summary>
    /// Gets the completed invocation count.
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Gets the successful tool result count.
    /// </summary>
    public long SuccessCount { get; init; }

    /// <summary>
    /// Gets the failed tool result count.
    /// </summary>
    public long ErrorCount { get; init; }

    /// <summary>
    /// Gets the failure ratio or zero for an empty range.
    /// </summary>
    public double ErrorRate { get; init; }
}
