namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Contains fixed tool metadata and authoritative live state.
/// </summary>
public sealed record McpToolResponseDto
{
    /// <summary>
    /// Gets the public leaf name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the English display title.
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// Gets the capability summary.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Gets Owner-only routing metadata when applicable.
    /// </summary>
    public ToolImplementationResponseDto? Implementation { get; init; }

    /// <summary>
    /// Gets the live global enablement.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the last UTC state change.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the changing Owner or null after seed.
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Gets the opaque expected version.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
