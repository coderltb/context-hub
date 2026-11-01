namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Returns the common envelope after the shared authorization and validation pipeline.
/// </summary>
/// <param name="mcpPromptService">The configuration persistence port.</param>
public sealed class GetMcpPromptQueryHandler(IMcpPromptService mcpPromptService)
    : IRequestHandler<GetMcpPromptQuery, ApiResponse<McpPromptResponseDto>>
{
    /// <summary>
    /// Dispatches the approved configuration operation.
    /// </summary>
    /// <param name="request">The validated request.</param>
    /// <param name="cancellationToken">Cancels persistence work.</param>
    /// <returns>The handler-owned envelope.</returns>
    public async ValueTask<ApiResponse<McpPromptResponseDto>> Handle(
        GetMcpPromptQuery request,
        CancellationToken cancellationToken)
    {
        var result = await mcpPromptService.GetAsync(request.Id, cancellationToken);

        return new(result);
    }
}
