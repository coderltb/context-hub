namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Confirms queue acceptance without asserting that the reported issue was verified.
/// </summary>
public sealed record QualitySignalResponseDto
{
    /// <summary>
    /// Gets accepted or deduplicated.
    /// </summary>
    public string Status { get; init; } = "";
}
