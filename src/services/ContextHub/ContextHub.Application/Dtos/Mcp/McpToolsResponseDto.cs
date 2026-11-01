namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Lists all registered tools in fixed discovery order.
/// </summary>
public sealed record McpToolsResponseDto
{
    /// <summary>
    /// Gets all registered live tool states.
    /// </summary>
    public IReadOnlyList<McpToolResponseDto> Items { get; init; } = [];
}
