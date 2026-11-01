namespace ContextHub.Infrastructure.Interfaces.Integration;

/// <summary>
/// Uses the native MCP client behind bounded endpoint and response checks.
/// </summary>
public interface IExternalMcpClient
{
    /// <summary>
    /// Discovers bounded definitions using the native protocol.
    /// </summary>
    /// <param name="endpoint">The checked HTTPS endpoint.</param>
    /// <param name="cancellationToken">Cancels protocol and content reads.</param>
    /// <returns>The private upstream definitions.</returns>
    Task<IReadOnlyList<ExternalToolModel>> DiscoverAsync(string endpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Calls one exact upstream tool without retry or hedging.
    /// </summary>
    /// <param name="endpoint">The checked HTTPS endpoint.</param>
    /// <param name="toolName">The reviewed upstream tool name.</param>
    /// <param name="arguments">The exact schema-validated argument object.</param>
    /// <param name="cancellationToken">Cancels the native protocol call.</param>
    /// <returns>The native result serialized with protocol options.</returns>
    Task<JsonElement> CallAsync(string endpoint, string toolName, JsonElement arguments, CancellationToken cancellationToken);
}
