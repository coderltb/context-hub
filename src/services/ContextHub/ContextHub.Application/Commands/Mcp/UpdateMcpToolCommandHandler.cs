namespace ContextHub.Application.Commands.Mcp;

/// <summary>
/// Owns the use-case result while Infrastructure performs scoped persistence.
/// </summary>
/// <param name="featureService">The feature port receiving Application contracts only.</param>
public sealed class UpdateMcpToolCommandHandler(IToolStateService featureService) : IRequestHandler<UpdateMcpToolCommand, ApiResponse<McpToolResponseDto>>
{
    /// <summary>
    /// Executes the authorized operation and creates the shared envelope once.
    /// </summary>
    /// <param name="request">The validated use-case input.</param>
    /// <param name="cancellationToken">Cancels the feature operation.</param>
    /// <returns>The approved result in the common envelope.</returns>
    public async ValueTask<ApiResponse<McpToolResponseDto>> Handle(
        UpdateMcpToolCommand request,
        CancellationToken cancellationToken)
    {
        var result = await featureService.UpdateAsync(request.ToolName, request.Request, cancellationToken);

        return new(result);
    }
}
