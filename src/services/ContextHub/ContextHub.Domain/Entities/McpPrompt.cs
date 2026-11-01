namespace ContextHub.Domain.Entities;

/// <summary>
/// Stores guidance configuration independently of caller inputs and rendered messages.
/// </summary>
public sealed class McpPrompt : BaseEntity<Guid>
{
    /// <summary>
    /// Gets or sets the immutable MCP name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets optional discovery title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets optional discovery description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets plain guidance text.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Gets or sets ordered argument definitions as jsonb.
    /// </summary>
    public string Arguments { get; set; } = "[]";

    /// <summary>
    /// Gets or sets whether the prompt is available.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the native PostgreSQL xmin version.
    /// </summary>
    public uint ConcurrencyVersion { get; set; }
}
