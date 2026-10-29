namespace ContextHub.Authentication.Api.Controllers.Admin;

/// <summary>
/// Binds approved administrative account contracts without exposing native Identity entities.
/// </summary>
/// <param name="mediator">The Application dispatcher with shared authorization and validation behaviors.</param>
[Route(ApiRouteConstants.USERS)]
[Authorize(Policy = AuthenticationConstants.OWNER_POLICY)]
public sealed class UsersController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Gets fresh approved information for the authenticated Owner.
    /// </summary>
    /// <param name="cancellationToken">Cancels account reads.</param>
    /// <returns>The current profile inside the shared envelope.</returns>
    [HttpGet(ApiRouteConstants.CURRENT_USER)]
    [ProducesResponseType<ApiResponse<UserResponseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new GetCurrentUserQuery(), cancellationToken));

    /// <summary>
    /// Changes the authenticated Owner's password using native current-password verification.
    /// </summary>
    /// <remarks>
    /// A successful change revokes the account's Owner grants; client credentials remain independent.
    /// </remarks>
    /// <param name="request">Current credential proof and the replacement password.</param>
    /// <param name="cancellationToken">Cancels the atomic change and human-grant revocation.</param>
    /// <returns>The safe account profile after persistence succeeds.</returns>
    [HttpPost(ApiRouteConstants.CURRENT_USER_PASSWORD)]
    [ProducesResponseType<ApiResponse<UserResponseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new ChangePasswordCommand(request), cancellationToken));

    /// <summary>
    /// Gets the requested account page using explicit searchable fields and stable ordering.
    /// </summary>
    /// <remarks>
    /// Text search and the optional status filter are combined with AND. Each sort field has its own direction.
    /// </remarks>
    /// <param name="request">The grouped pagination, search, filter and ordering values.</param>
    /// <param name="cancellationToken">Cancels the database reads.</param>
    /// <returns>The shared page inside the API envelope.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PageResponse<UserResponseDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] GetUsersRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new GetUsersQuery(request), cancellationToken));

    /// <summary>
    /// Gets one approved account projection.
    /// </summary>
    /// <param name="userId">The account identifier.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The approved account response.</returns>
    [HttpGet(ApiRouteConstants.USER_ID)]
    [ProducesResponseType<ApiResponse<UserResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new GetUserQuery(userId), cancellationToken));

    /// <summary>
    /// Creates an Owner using a password handled only by native Identity.
    /// </summary>
    /// <remarks>
    /// This anonymous API creates an Owner using username, email and password; it is separate from token-only clients.
    /// </remarks>
    /// <param name="request">The public account metadata.</param>
    /// <param name="cancellationToken">Cancels provisioning.</param>
    /// <returns>The approved account without password material.</returns>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<UserResponseDto>>(StatusCodes.Status201Created)]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None,
        Order = ApiValueConstants.SENSITIVE_RESPONSE_CACHE_ORDER)]
    public async Task<IActionResult> Create(CreateUserRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateUserCommand(request), cancellationToken);

        return CreatedAtAction(nameof(Get), new { userId = result.Data!.Id }, result);
    }

    /// <summary>
    /// Creates a passwordless account through the explicit invitation capability.
    /// </summary>
    /// <remarks>
    /// The returned setup URL is disclosed only at issuance and expires after thirty minutes.
    /// </remarks>
    /// <param name="request">The invited account metadata and approved role selection.</param>
    /// <param name="cancellationToken">Cancels native provisioning.</param>
    /// <returns>The account and one-time invitation envelope.</returns>
    [HttpPost(ApiRouteConstants.USER_INVITATIONS)]
    [ProducesResponseType<ApiResponse<InvitedUserResponseDto>>(StatusCodes.Status201Created)]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None,
        Order = ApiValueConstants.SENSITIVE_RESPONSE_CACHE_ORDER)]
    public async Task<IActionResult> InviteUser(
        InviteUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new InviteUserCommand(request), cancellationToken);

        return CreatedAtAction(nameof(Get), new { userId = result.Data!.User.Id }, result);
    }

    /// <summary>
    /// Updates account access under the last-active-Owner invariant.
    /// </summary>
    /// <param name="userId">The target account.</param>
    /// <param name="request">The new state and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The latest approved account state.</returns>
    [HttpPut(ApiRouteConstants.USER_STATUS)]
    [OpenApiResponseExample<DisabledUserResponseExample>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<UserResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetStatus(
        Guid userId,
        UserStatusRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new SetUserStatusCommand(userId, request), cancellationToken));

    /// <summary>
    /// Issues a replacement invitation without creating a password-reset API.
    /// </summary>
    /// <param name="userId">The account awaiting setup.</param>
    /// <param name="request">The expected account version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The replacement one-time URL.</returns>
    [HttpPost(ApiRouteConstants.USER_INVITATION)]
    [ProducesResponseType<ApiResponse<InvitationResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None,
        Order = ApiValueConstants.SENSITIVE_RESPONSE_CACHE_ORDER)]
    public async Task<IActionResult> Invite(
        Guid userId,
        InvitationRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RegenerateInvitationCommand(userId, request), cancellationToken);

        return Ok(result);
    }
}
