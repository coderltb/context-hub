namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Requests current prompt configuration for an authenticated MCP client.
/// </summary>
public sealed record ListMcpPromptsQuery : IRequest<IReadOnlyList<McpPromptModel>>, IMcpRequest
{
    /// <summary>
    /// Gets an empty selection because prompts never invoke a tool.
    /// </summary>
    public string ToolName => "";
}
