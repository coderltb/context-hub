namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Replaces editable repository metadata with optimistic concurrency.
/// </summary>
public sealed class UpdateRepositoryRequestDto
{
    /// <summary>
    /// Gets the replacement GitHub repository URL, normalized before persistence.
    /// </summary>
    public string RepositoryUrl { get; init; } = "";

    /// <summary>
    /// Gets the branch, tag or commit used by explicit sync.
    /// </summary>
    public string DefaultRef { get; init; } = "";

    /// <summary>
    /// Gets whether the source may be read.
    /// </summary>
    [JsonRequired]
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets the opaque version returned by the latest read.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";

}
