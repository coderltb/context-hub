namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Carries the public library-documentation input without exposing protocol objects to the service port.
/// </summary>
public sealed record LibraryDocumentationSearchRequestDto
{
    /// <summary>
    /// Gets the public library name to resolve.
    /// </summary>
    public string LibraryName { get; init; } = "";

    /// <summary>
    /// Gets the public documentation topic.
    /// </summary>
    public string Topic { get; init; } = "";

    /// <summary>
    /// Gets an optional exact library version.
    /// </summary>
    public string? Version { get; init; }
}
