namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Returns the common envelope after the shared authorization and validation pipeline.
/// </summary>
/// <param name="mcpPromptService">The configuration persistence port.</param>
public sealed class GetMcpPromptsQueryHandler(IMcpPromptService mcpPromptService)
    : IRequestHandler<GetMcpPromptsQuery, ApiResponse<PageResponse<McpPromptSummaryResponseDto>>>
{
    /// <summary>
    /// Dispatches the approved configuration operation.
    /// </summary>
    /// <param name="request">The validated request.</param>
    /// <param name="cancellationToken">Cancels persistence work.</param>
    /// <returns>The handler-owned envelope.</returns>
    public async ValueTask<ApiResponse<PageResponse<McpPromptSummaryResponseDto>>> Handle(
        GetMcpPromptsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await mcpPromptService.GetPageAsync(request.Request, cancellationToken);

        return new(result);
    }
}
