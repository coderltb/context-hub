namespace ContextHub.Application.Models.Mcp;

/// <summary>
/// Carries a detached configuration snapshot without any caller argument values.
/// </summary>
public sealed record McpPromptModel
{
    /// <summary>
    /// Gets the stable MCP name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the optional native title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the optional discovery description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets guidance text.
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// Gets ordered configuration definitions.
    /// </summary>
    public IReadOnlyList<McpPromptArgumentDto> Arguments { get; init; } = [];
}
