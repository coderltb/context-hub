namespace ContextHub.Application.Commands.Mcp;

/// <summary>
/// Returns the common envelope after the shared authorization and validation pipeline.
/// </summary>
/// <param name="mcpPromptService">The configuration persistence port.</param>
public sealed class UpdateMcpPromptCommandHandler(IMcpPromptService mcpPromptService)
    : IRequestHandler<UpdateMcpPromptCommand, ApiResponse<McpPromptResponseDto>>
{
    /// <summary>
    /// Dispatches the approved configuration operation.
    /// </summary>
    /// <param name="request">The validated request.</param>
    /// <param name="cancellationToken">Cancels persistence work.</param>
    /// <returns>The handler-owned envelope.</returns>
    public async ValueTask<ApiResponse<McpPromptResponseDto>> Handle(
        UpdateMcpPromptCommand request,
        CancellationToken cancellationToken)
    {
        var result = await mcpPromptService.UpdateAsync(request.Id, request.Request, cancellationToken);

        return new(result);
    }
}
