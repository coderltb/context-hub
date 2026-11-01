namespace ContextHub.Domain.Entities;

/// <summary>
/// Owns the live global enablement and paired JSON contracts of one registered MCP tool.
/// </summary>
public sealed class Tool : BaseEntity<Guid>
{
    /// <summary>
    /// Gets or sets the stable tool leaf name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the current display title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the current discovery summary.
    /// </summary>
    public string Description { get; set; } = "";


    /// <summary>
    /// Gets or sets whether this tool is globally enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the current input JSON Schema stored as PostgreSQL jsonb.
    /// </summary>
    public string InputSchema { get; set; } = "";

    /// <summary>
    /// Gets or sets the output JSON Schema paired with the input contract.
    /// </summary>
    public string OutputSchema { get; set; } = "";

    /// <summary>
    /// Gets or sets the PostgreSQL optimistic concurrency version.
    /// </summary>
    public uint ConcurrencyVersion { get; set; }
}
