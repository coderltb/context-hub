namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Reads current enabled metadata without a cross-request configuration cache.
/// </summary>
/// <param name="mcpPromptService">The configuration reader.</param>
public sealed class ListMcpPromptsQueryHandler(IMcpPromptService mcpPromptService)
    : IRequestHandler<ListMcpPromptsQuery, IReadOnlyList<McpPromptModel>>
{
    /// <summary>
    /// Reads enabled definitions after the shared MCP authorization policy.
    /// </summary>
    /// <param name="request">The authenticated discovery request.</param>
    /// <param name="cancellationToken">Cancels the authority query.</param>
    /// <returns>The enabled definitions in name order.</returns>
    public async ValueTask<IReadOnlyList<McpPromptModel>> Handle(
        ListMcpPromptsQuery request,
        CancellationToken cancellationToken) =>
        await mcpPromptService.GetEnabledAsync(cancellationToken);
}
