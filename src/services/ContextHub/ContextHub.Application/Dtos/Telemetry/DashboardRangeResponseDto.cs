namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains privacy-safe dashboard aggregates.
/// </summary>
public sealed record DashboardRangeResponseDto
{
    /// <summary>
    /// Gets the inclusive first UTC date.
    /// </summary>
    public DateOnly From { get; init; }

    /// <summary>
    /// Gets the inclusive last UTC date.
    /// </summary>
    public DateOnly To { get; init; }
}
