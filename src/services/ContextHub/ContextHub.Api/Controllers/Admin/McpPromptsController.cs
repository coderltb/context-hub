namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Manages stored prompt configuration using Owner database identifiers.
/// </summary>
/// <param name="mediator">The shared scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.MCP_PROMPTS)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
[ApiMetadataOnlyLogging]
public sealed class McpPromptsController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists prompt metadata with search and deterministic ordering.
    /// </summary>
    /// <param name="request">The grouped list input.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The metadata page.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PageResponse<McpPromptSummaryResponseDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] GetMcpPromptsRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new GetMcpPromptsQuery(request), cancellationToken));

    /// <summary>
    /// Gets the complete configuration selected by its database identifier.
    /// </summary>
    /// <param name="id">The prompt database identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The configuration or a safe not-found response.</returns>
    [HttpGet(AdminRouteConstants.PROMPT_ID)]
    [ProducesResponseType<ApiResponse<McpPromptResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new GetMcpPromptQuery(id), cancellationToken));

    /// <summary>
    /// Creates a named prompt that becomes available without rebuilding the server.
    /// </summary>
    /// <param name="request">The guidance configuration, without consumer values.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The created configuration and concurrency token.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<McpPromptResponseDto>>(StatusCodes.Status201Created)]
    [OpenApiOperation(StatusCodes.Status201Created, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMcpPromptRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateMcpPromptCommand(request), cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Changes mutable configuration or disables a prompt using its expected version.
    /// </summary>
    /// <param name="id">The prompt database identifier.</param>
    /// <param name="request">The configuration and version from a preceding read.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The committed configuration.</returns>
    [HttpPut(AdminRouteConstants.PROMPT_ID)]
    [ProducesResponseType<ApiResponse<McpPromptResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateMcpPromptRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new UpdateMcpPromptCommand(id, request), cancellationToken));
}
