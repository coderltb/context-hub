namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Adapts protected McpTools operations to the shared Application dispatcher.
/// </summary>
/// <param name="mediator">The scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.MCP_TOOLS)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
public sealed class McpToolsController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists the fixed capabilities and their current global state.
    /// </summary>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<McpToolsResponseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMcpToolsQuery(), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Changes global tool enablement without granting client permissions.
    /// </summary>
    /// <param name="toolName">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPut(AdminRouteConstants.TOOL_NAME)]
    [ProducesResponseType<ApiResponse<McpToolResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        string toolName,
        [FromBody] UpdateMcpToolRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UpdateMcpToolCommand(toolName, request), cancellationToken);

        return Ok(result);
    }

}
