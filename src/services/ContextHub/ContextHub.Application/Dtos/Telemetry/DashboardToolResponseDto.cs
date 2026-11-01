namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains privacy-safe dashboard aggregates.
/// </summary>
public sealed record DashboardToolResponseDto
{
    /// <summary>
    /// Gets the fixed public tool name.
    /// </summary>
    public string ToolName { get; init; } = "";

    /// <summary>
    /// Gets the completed count.
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// Gets the successful count.
    /// </summary>
    public long SuccessCount { get; init; }

    /// <summary>
    /// Gets the failed count.
    /// </summary>
    public long ErrorCount { get; init; }

    /// <summary>
    /// Gets the failure ratio.
    /// </summary>
    public double ErrorRate { get; init; }
}
