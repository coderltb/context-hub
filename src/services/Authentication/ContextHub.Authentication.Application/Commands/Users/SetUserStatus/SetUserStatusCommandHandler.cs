namespace ContextHub.Authentication.Application.Commands.Users.SetUserStatus;

/// <summary>
/// Orchestrates the authorized account operation through the Application-owned port.
/// </summary>
/// <param name="userService">The account boundary implemented using native Identity managers.</param>
public sealed class SetUserStatusCommandHandler(IUserService userService)
    : IRequestHandler<SetUserStatusCommand, ApiResponse<UserResponseDto>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels account reads or writes.</param>
    /// <returns>The approved account response.</returns>
    public async ValueTask<ApiResponse<UserResponseDto>> Handle(
        SetUserStatusCommand request,
        CancellationToken cancellationToken)
    {
        // The service enforces the final-active-Owner invariant and invalidates grants before returning persisted state.
        var result = await userService.SetStatusAsync(request.UserId, request.Request, cancellationToken);

        return new(result);
    }
}
