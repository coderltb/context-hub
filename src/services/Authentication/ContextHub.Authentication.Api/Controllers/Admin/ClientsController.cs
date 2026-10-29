namespace ContextHub.Authentication.Api.Controllers.Admin;

/// <summary>
/// Exposes application credential administration only to an authenticated Owner.
/// </summary>
/// <param name="mediator">The Application dispatcher with shared authorization and validation behaviors.</param>
[Route(ApiRouteConstants.CLIENTS)]
[Authorize(Policy = AuthenticationConstants.OWNER_POLICY)]
public sealed class ClientsController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists metadata without returning any stored digest or credential value.
    /// </summary>
    /// <param name="request">The grouped public list inputs.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe application page.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PageResponse<ClientResponseDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] GetClientsRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new GetClientsQuery(request), cancellationToken));

    /// <summary>
    /// Gets the application and its independently revocable credential metadata.
    /// </summary>
    /// <param name="clientId">The application identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe detail response.</returns>
    [HttpGet(ApiRouteConstants.CLIENT_ID)]
    [ProducesResponseType<ApiResponse<ClientDetailResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid clientId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetClientQuery(clientId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Creates an application and returns its first raw token exactly once.
    /// </summary>
    /// <remarks>
    /// Copy the returned raw token now; later reads expose only metadata. Null expiry means no fixed expiration.
    /// </remarks>
    /// <param name="request">The required application metadata.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The created application's first credential.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<IssuedClientTokenResponseDto>>(StatusCodes.Status201Created)]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None,
        Order = ApiValueConstants.SENSITIVE_RESPONSE_CACHE_ORDER)]
    public async Task<IActionResult> Create(CreateClientRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateClientCommand(request), cancellationToken);

        return CreatedAtAction(nameof(Get), new { clientId = result.Data!.ClientId }, result);
    }

    /// <summary>
    /// Changes application status and metadata using optimistic concurrency.
    /// </summary>
    /// <param name="clientId">The application identifier.</param>
    /// <param name="request">The desired metadata and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The updated application projection.</returns>
    [HttpPut(ApiRouteConstants.CLIENT_ID)]
    [ProducesResponseType<ApiResponse<ClientResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid clientId,
        UpdateClientRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new UpdateClientCommand(clientId, request), cancellationToken));

    /// <summary>
    /// Issues a replacement token while preserving explicit overlap with existing credentials.
    /// </summary>
    /// <remarks>
    /// AllowedTools belongs to this new token. Existing tokens remain valid until independently revoked, expired or disabled.
    /// </remarks>
    /// <param name="clientId">The receiving application.</param>
    /// <param name="request">The explicit tool grants for the new credential.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The one-time raw credential.</returns>
    [HttpPost(ApiRouteConstants.CLIENT_TOKENS)]
    [OpenApiResponseExample<IssuedExpiringClientTokenResponseExample>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<IssuedClientTokenResponseDto>>(StatusCodes.Status201Created)]
    [OpenApiOperation(StatusCodes.Status201Created, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None,
        Order = ApiValueConstants.SENSITIVE_RESPONSE_CACHE_ORDER)]
    public async Task<IActionResult> Issue(
        Guid clientId,
        IssueClientTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new IssueClientTokenCommand(clientId, request), cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Revokes one credential without revoking other credentials belonging to the same application.
    /// </summary>
    /// <remarks>
    /// Repeated revocation preserves the original revocation metadata.
    /// </remarks>
    /// <param name="clientId">The owning application.</param>
    /// <param name="tokenId">The credential identifier.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The safe revocation metadata.</returns>
    [HttpPost(ApiRouteConstants.CLIENT_TOKEN_REVOKE)]
    [ProducesResponseType<ApiResponse<ClientTokenResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid clientId,
        Guid tokenId,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new RevokeClientTokenCommand(clientId, tokenId), cancellationToken));
}
