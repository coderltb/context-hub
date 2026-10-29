namespace ContextHub.Authentication.Application.Commands.Roles.ReplaceUserRoles;

/// <summary>
/// Orchestrates the authorized account operation through the Application-owned port.
/// </summary>
/// <param name="userService">The account boundary implemented using native Identity managers.</param>
public sealed class ReplaceUserRolesCommandHandler(IUserService userService)
    : IRequestHandler<ReplaceUserRolesCommand, ApiResponse<UserResponseDto>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels account reads or writes.</param>
    /// <returns>The approved account response.</returns>
    public async ValueTask<ApiResponse<UserResponseDto>> Handle(
        ReplaceUserRolesCommand request,
        CancellationToken cancellationToken)
    {
        // Native role changes and affected-grant revocation commit together inside the account service.
        var result = await userService.ReplaceRolesAsync(request.UserId, request.Request, cancellationToken);

        return new(result);
    }
}
