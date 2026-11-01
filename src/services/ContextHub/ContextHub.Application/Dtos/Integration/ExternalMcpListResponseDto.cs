namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Contains saved connections and derived discovery metadata.
/// </summary>
public sealed class ExternalMcpListResponseDto
{
    /// <summary>
    /// Gets the saved Owner-only connections.
    /// </summary>
    public IReadOnlyList<ExternalMcpResponseDto> Items { get; init; } = [];
}
