namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Declares the Owner-approved GitHub source metadata.
/// </summary>
public sealed class CreateRepositoryRequestDto
{
    /// <summary>
    /// Gets the canonical HTTPS GitHub repository URL.
    /// </summary>
    public string RepositoryUrl { get; init; } = "";

    /// <summary>
    /// Gets the bounded branch, tag or commit used by explicit sync.
    /// </summary>
    public string DefaultRef { get; init; } = "";

    /// <summary>
    /// Gets whether this repository may be read.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

}
