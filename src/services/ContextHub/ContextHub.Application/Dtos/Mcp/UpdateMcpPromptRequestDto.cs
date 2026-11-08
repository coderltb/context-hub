namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record UpdateMcpPromptRequestDto
{
    /// <summary>
    /// Gets saved argument IDs explicitly removed by this atomic update.
    /// </summary>
    public IReadOnlyList<Guid> RemovedArgumentIds { get; init; } = [];

    /// <summary>
    /// Gets the optional display title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the optional discovery description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets plain guidance text; no template code is executed.
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// Gets ordered argument definitions, never submitted argument values.
    /// </summary>
    public IReadOnlyList<McpPromptArgumentDto?> Arguments { get; init; } = [];

    /// <summary>
    /// Gets whether discovery and retrieval are enabled.
    /// </summary>
    [JsonRequired]
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets the opaque version returned by the preceding read.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
