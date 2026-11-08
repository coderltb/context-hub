namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Returns the authorized configuration inside the shared REST envelope.
/// </summary>
/// <param name="toolStateService">The current tool configuration port.</param>
public sealed class GetMcpToolQueryHandler(IToolStateService toolStateService)
    : IRequestHandler<GetMcpToolQuery, ApiResponse<McpToolDetailResponseDto>>
{
    /// <summary>
    /// Reads one configuration without executing a tool or granting access to it.
    /// </summary>
    /// <param name="request">The selected tool identity.</param>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>The complete Owner detail.</returns>
    public async ValueTask<ApiResponse<McpToolDetailResponseDto>> Handle(
        GetMcpToolQuery request,
        CancellationToken cancellationToken)
    {
        var result = await toolStateService.GetAsync(request.ToolId, cancellationToken);

        return new(result);
    }
}
