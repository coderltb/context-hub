namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Adapts protected ExternalMcps operations to the shared Application dispatcher.
/// </summary>
/// <param name="mediator">The scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.EXTERNAL_MCPS)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
[ApiMetadataOnlyLogging]
public sealed class ExternalMcpsController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists managed connection settings and derived discovery metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<ExternalMcpListResponseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetExternalMcpsQuery(), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Normalizes imported connection settings without persisting credentials.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost(AdminRouteConstants.PREVIEW)]
    [ProducesResponseType<ApiResponse<ExternalMcpPreviewResponseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        [FromBody] JsonElement request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new PreviewExternalMcpsCommand(request), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Creates a new reviewed canonical connection.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<ExternalMcpResponseDto>>(StatusCodes.Status201Created)]
    [OpenApiOperation(StatusCodes.Status201Created, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateExternalMcpRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateExternalMcpCommand(request), cancellationToken);
        Response.Headers.Location = "/" + AdminRouteConstants.EXTERNAL_MCPS + "/" + result.Data!.ConnectionId;

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Updates the selected connection without changing its UUID.
    /// </summary>
    /// <param name="connectionId">The stored connection identity.</param>
    /// <param name="request">The editable label, configuration and concurrency token.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The committed Owner projection.</returns>
    [HttpPut(AdminRouteConstants.CONNECTION_ID)]
    [ProducesResponseType<ApiResponse<ExternalMcpResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Save(
        Guid connectionId,
        [FromBody] SaveExternalMcpRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new SaveExternalMcpCommand(connectionId, request),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Disables routing for the selected UUID while retaining its configuration and creation audit.
    /// </summary>
    /// <param name="connectionId">The exact route or concurrency identifier.</param>
    /// <param name="concurrencyToken">The exact route or concurrency identifier.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpDelete(AdminRouteConstants.CONNECTION_ID)]
    [ProducesResponseType<ApiResponse<DeletedIntegrationResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid connectionId,
        [FromHeader(Name = HubConstants.IF_MATCH_HEADER)] string concurrencyToken,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new DeleteExternalMcpCommand(connectionId, concurrencyToken.Trim('"')),
            cancellationToken);

        return Ok(result);
    }

}
