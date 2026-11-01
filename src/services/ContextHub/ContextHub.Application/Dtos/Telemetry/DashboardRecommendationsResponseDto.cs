namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains privacy-safe dashboard aggregates.
/// </summary>
public sealed record DashboardRecommendationsResponseDto
{
    /// <summary>
    /// Gets the new or accepted recommendation count.
    /// </summary>
    public long OpenCount { get; init; }
}
