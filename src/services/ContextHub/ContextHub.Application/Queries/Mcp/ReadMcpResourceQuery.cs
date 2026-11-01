namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Requests public content through the same associated tool grant and live catalog authority.
/// </summary>
/// <param name="Resource">The recognized fixed resource route.</param>
public sealed record ReadMcpResourceQuery(McpResourceRequestModel Resource) : IRequest<JsonNode>, IMcpRequest
{
    /// <summary>
    /// Gets the capability associated with the resource route.
    /// </summary>
    public string ToolName => Resource.ToolName;
}
