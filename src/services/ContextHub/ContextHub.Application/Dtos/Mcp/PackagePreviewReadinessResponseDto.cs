namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackagePreviewReadinessResponseDto
{
    /// <summary>
    /// Gets ready or blocked from actual findings.
    /// </summary>
    public string Status { get; init; } = "blocked";

    /// <summary>
    /// Gets safe validation findings.
    /// </summary>
    public IReadOnlyList<PreviewFindingResponseDto> Findings { get; init; } = [];

    /// <summary>
    /// Gets the candidate hash when available.
    /// </summary>
    public string? CandidateContentHash { get; init; }
}
