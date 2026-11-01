namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Describes Owner-only adapter routing without provider credentials.
/// </summary>
public sealed record ToolImplementationResponseDto
{
    /// <summary>
    /// Gets the adapter kind.
    /// </summary>
    public string Kind { get; init; } = "managed_external_mcp";

    /// <summary>
    /// Gets the currently eligible connection names.
    /// </summary>
    public IReadOnlyList<string> ConnectionNames { get; init; } = [];
}
