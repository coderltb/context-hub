namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Requests a workflow transition using an expected database version.
/// </summary>
public sealed record UpdateRecommendationStatusRequestDto
{
    /// <summary>
    /// Gets the target workflow state.
    /// </summary>
    public string Status { get; init; } = "";

    /// <summary>
    /// Gets the opaque expected version.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
