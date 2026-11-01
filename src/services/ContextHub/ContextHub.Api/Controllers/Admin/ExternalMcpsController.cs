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
    /// Creates or updates a reviewed canonical connection.
    /// </summary>
    /// <param name="name">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPut(AdminRouteConstants.CONNECTION_NAME)]
    [ProducesResponseType<ApiResponse<ExternalMcpResponseDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<ExternalMcpResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Save(
        string name,
        [FromBody] SaveExternalMcpRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new SaveExternalMcpCommand(name, request), cancellationToken);

        return request.ConcurrencyToken is null ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
    }

    /// <summary>
    /// Removes the matching current connection and invalidates its discovery.
    /// </summary>
    /// <param name="name">The exact route or concurrency identifier.</param>
    /// <param name="concurrencyToken">The exact route or concurrency identifier.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpDelete(AdminRouteConstants.CONNECTION_NAME)]
    [ProducesResponseType<ApiResponse<DeletedIntegrationResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        string name,
        [FromHeader(Name = HubConstants.IF_MATCH_HEADER)] string concurrencyToken,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteExternalMcpCommand(name, concurrencyToken.Trim('"')), cancellationToken);

        return Ok(result);
    }

}
