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
    /// Shows the stored metadata, schemas and related resource routes for one tool.
    /// </summary>
    /// <param name="toolId">The stable tool UUID.</param>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>The handler-owned Owner detail.</returns>
    [HttpGet(AdminRouteConstants.TOOL_ID)]
    [ProducesResponseType<ApiResponse<McpToolDetailResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid toolId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMcpToolQuery(toolId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Lists the fixed capabilities and their current global state.
    /// </summary>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    /// <param name="request">The bounded metadata filters and page.</param>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PageResponse<McpToolResponseDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] GetMcpToolsRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMcpToolsQuery(request), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Changes global tool enablement without granting client permissions.
    /// </summary>
    /// <param name="toolId">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPut(AdminRouteConstants.TOOL_ID)]
    [ProducesResponseType<ApiResponse<McpToolResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid toolId,
        [FromBody] UpdateMcpToolRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UpdateMcpToolCommand(toolId, request), cancellationToken);

        return Ok(result);
    }

}
