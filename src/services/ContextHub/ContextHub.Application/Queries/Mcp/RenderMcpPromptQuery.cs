namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Requests current prompt configuration for an authenticated MCP client.
/// </summary>
/// <param name="Name">The request-local protocol input.</param>
/// <param name="Arguments">The request-local protocol input.</param>
public sealed record RenderMcpPromptQuery(
    string Name,
    IDictionary<string, JsonElement>? Arguments) : IRequest<McpPromptMessageModel>, IMcpRequest
{
    /// <summary>
    /// Gets an empty selection because prompts never invoke a tool.
    /// </summary>
    public string ToolName => "";
}
