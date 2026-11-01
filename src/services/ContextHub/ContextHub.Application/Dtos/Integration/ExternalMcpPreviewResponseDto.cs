namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Contains preview-only normalized imports without persisted changes.
/// </summary>
public sealed class ExternalMcpPreviewResponseDto
{
    /// <summary>
    /// Gets the normalized preview items.
    /// </summary>
    public IReadOnlyList<ExternalMcpPreviewItemDto> Items { get; init; } = [];
}
