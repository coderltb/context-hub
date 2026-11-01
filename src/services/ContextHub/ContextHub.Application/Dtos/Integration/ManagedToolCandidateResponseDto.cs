namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Describes one ranked capability using only reviewed public metadata.
/// </summary>
public sealed record ManagedToolCandidateResponseDto
{
    /// <summary>
    /// Gets the opaque routing reference.
    /// </summary>
    public string ToolRef { get; init; } = "";

    /// <summary>
    /// Gets the reviewed public title.
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// Gets the reviewed capability summary.
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// Gets the branches that actually matched.
    /// </summary>
    public IReadOnlyList<string> MatchReasons { get; init; } = [];

    /// <summary>
    /// Gets the required input field names.
    /// </summary>
    public IReadOnlyList<string> InputOverview { get; init; } = [];
}

