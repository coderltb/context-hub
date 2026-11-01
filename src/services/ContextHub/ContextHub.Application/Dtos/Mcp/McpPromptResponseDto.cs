namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record McpPromptResponseDto : McpPromptSummaryResponseDto
{
    /// <summary>
    /// Gets plain guidance text; no template code is executed.
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// Gets ordered argument definitions, never submitted argument values.
    /// </summary>
    public IReadOnlyList<McpPromptArgumentDto> Arguments { get; init; } = [];
}
