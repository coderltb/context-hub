namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Owns the use-case result while Infrastructure performs scoped persistence.
/// </summary>
/// <param name="featureService">The feature port receiving Application contracts only.</param>
public sealed class GetMcpToolsQueryHandler(IToolStateService featureService) : IRequestHandler<GetMcpToolsQuery, ApiResponse<PageResponse<McpToolResponseDto>>>
{
    /// <summary>
    /// Executes the authorized operation and creates the shared envelope once.
    /// </summary>
    /// <param name="request">The validated use-case input.</param>
    /// <param name="cancellationToken">Cancels the feature operation.</param>
    /// <returns>The approved result in the common envelope.</returns>
    public async ValueTask<ApiResponse<PageResponse<McpToolResponseDto>>> Handle(
        GetMcpToolsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await featureService.GetListAsync(request.Request, cancellationToken);

        return new(result);
    }
}
