namespace ContextHub.Authentication.Application.Commands.Users.InviteUser;

/// <summary>
/// Orchestrates the authorized account operation through the Application-owned port.
/// </summary>
/// <param name="userService">The account boundary implemented using native Identity managers.</param>
public sealed class InviteUserCommandHandler(IUserService userService)
    : IRequestHandler<InviteUserCommand, ApiResponse<InvitedUserResponseDto>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels account reads or writes.</param>
    /// <returns>The approved account response.</returns>
    public async ValueTask<ApiResponse<InvitedUserResponseDto>> Handle(
        InviteUserCommand request,
        CancellationToken cancellationToken)
    {
        // The account service creates the native user and invitation atomically; the link never enters diagnostics.
        var result = await userService.InviteAsync(request.Request, cancellationToken);

        return new(result);
    }
}
