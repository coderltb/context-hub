namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Returns bounded inspection metadata without persisting source contents.
/// </summary>
public sealed class RepositorySyncResponseDto
{
    /// <summary>
    /// Gets repository metadata after the committed sync result.
    /// </summary>
    public RepositoryResponseDto Repository { get; init; } = new();

    /// <summary>
    /// Gets the exact resolved commit.
    /// </summary>
    public string ResolvedCommitSha { get; init; } = "";

    /// <summary>
    /// Gets the number of supported source files inspected in this request.
    /// </summary>
    public int FilesInspected { get; init; }

    /// <summary>
    /// Gets safe warnings without private source details.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
