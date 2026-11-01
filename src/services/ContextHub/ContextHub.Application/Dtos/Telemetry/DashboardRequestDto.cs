namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Selects an inclusive UTC range with Application-owned defaults.
/// </summary>
public sealed record DashboardRequestDto
{
    /// <summary>
    /// Gets the optional first UTC date.
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// Gets the optional final UTC date.
    /// </summary>
    public DateOnly? To { get; init; }
}
