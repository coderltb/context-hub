namespace ContextHub.Domain.Entities;

/// <summary>
/// Stores Owner-approved external MCP connection configuration.
/// </summary>
public sealed class ExternalMcp : BaseEntity<Guid>
{
    /// <summary>
    /// Gets or sets the unique internal connection name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets retained lifecycle independently of Config.enabled.
    /// </summary>
    public ExternalMcpStatus Status { get; set; } = ExternalMcpStatus.Active;

    /// <summary>
    /// Gets or sets the canonical validated connection JSON.
    /// </summary>
    public string Config { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the PostgreSQL optimistic concurrency version.
    /// </summary>
    public uint ConcurrencyVersion { get; set; }
}
