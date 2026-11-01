namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Stores a closed canonical hosted MCP connection without credentials.
/// </summary>
public sealed class ExternalMcpConfigDto
{
    /// <summary>
    /// Gets the supported canonical schema version.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets the HTTPS hosted Streamable HTTP endpoint.
    /// </summary>
    public string Url { get; init; } = "";

    /// <summary>
    /// Gets the Owner-controlled enabled state.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the reviewed upstream tool-name policy.
    /// </summary>
    public ExternalToolPolicyDto ToolPolicy { get; init; } = new();

}
