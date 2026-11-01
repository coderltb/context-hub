namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Requests the intersection of current global tool state and the authenticated client's grants.
/// </summary>
public sealed record GetMcpDiscoveryQuery : IRequest<IReadOnlyList<string>>, IMcpRequest
{
    /// <summary>
    /// Gets an empty selection because discovery does not invoke an individual tool.
    /// </summary>
    public string ToolName => "";
}
