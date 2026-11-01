namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Combines canonical import data, discovered tools and compatibility warnings.
/// </summary>
public sealed class ExternalMcpPreviewItemDto
{
    /// <summary>
    /// Gets the Owner-supplied connection label.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets canonical validated connection settings.
    /// </summary>
    public ExternalMcpConfigDto Config { get; init; } = new();

    /// <summary>
    /// Gets bounded upstream tool metadata.
    /// </summary>
    public IReadOnlyList<ExternalToolResponseDto> Tools { get; init; } = [];

    /// <summary>
    /// Gets safe import normalization warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

}
