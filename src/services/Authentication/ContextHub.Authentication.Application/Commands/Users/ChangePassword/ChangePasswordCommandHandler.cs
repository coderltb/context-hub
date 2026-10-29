namespace ContextHub.Authentication.Application.Commands.Users.ChangePassword;

/// <summary>
/// Binds a password change to the trusted subject without exposing entities or native managers to Application.
/// </summary>
/// <param name="userService">The native account-operation boundary.</param>
/// <param name="authService">The shared authenticated-subject accessor.</param>
public sealed class ChangePasswordCommandHandler(IUserService userService, IAuthService authService)
    : IRequestHandler<ChangePasswordCommand, ApiResponse<UserResponseDto>>
{
    /// <summary>
    /// Delegates the atomic credential change and grant revocation after authorization and input validation.
    /// </summary>
    /// <param name="request">The credential input; no caller-selected account ID is accepted.</param>
    /// <param name="cancellationToken">Cancels the owned persistence work.</param>
    /// <returns>The safe account profile without password material.</returns>
    public async ValueTask<ApiResponse<UserResponseDto>> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        // The subject comes from the validated human principal, never the request body.
        var result = await userService.ChangePasswordAsync(authService.UserId!.Value, request.Request, cancellationToken);

        return new(result);
    }
}
