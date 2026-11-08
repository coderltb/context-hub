namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record McpPromptArgumentDto
{
    /// <summary>
    /// Gets the stored identity; absent for a new draft argument.
    /// </summary>
    public Guid? ArgumentId { get; init; }

    /// <summary>
    /// Gets whether the retained definition has been explicitly removed.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Gets the argument name supplied by MCP clients.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets optional instructions for this argument.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether the argument is required; omission remains optional.
    /// </summary>
    public bool? Required { get; init; }
}
