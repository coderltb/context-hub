namespace ContextHub.Authentication.Api.Controllers.Admin;

/// <summary>
/// Exposes provisioned roles and delegates membership invariants to account operations.
/// </summary>
/// <param name="mediator">The Application dispatcher with shared authorization and validation behaviors.</param>
[Route(ApiRouteConstants.ADMIN_V1)]
[Authorize(Policy = AuthenticationConstants.OWNER_POLICY)]
public sealed class RolesController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists only provisioned role metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approved role list.</returns>
    [HttpGet(ApiRouteConstants.ROLES)]
    [ProducesResponseType<ApiResponse<RoleListResponseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new GetRolesQuery(), cancellationToken));

    /// <summary>
    /// Replaces the account's complete membership set.
    /// </summary>
    /// <param name="userId">The target account.</param>
    /// <remarks>
    /// This replaces all memberships. Empty roles are allowed except when they would remove the last active Owner.
    /// </remarks>
    /// <param name="request">The complete role set and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The updated account projection.</returns>
    [HttpPut(ApiRouteConstants.USER_ROLES)]
    [ProducesResponseType<ApiResponse<UserResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Replace(
        Guid userId,
        UserRolesRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new ReplaceUserRolesCommand(userId, request), cancellationToken));
}
