namespace ContextHub.Authentication.Application.Queries.Users.GetCurrentUser;

/// <summary>
/// Resolves the approved profile using the identity already checked by the authorization pipeline.
/// </summary>
/// <param name="userService">The application account port returning approved projections.</param>
/// <param name="authService">The shared accessor for the authenticated human account.</param>
public sealed class GetCurrentUserQueryHandler(IUserService userService, IAuthService authService)
    : IRequestHandler<GetCurrentUserQuery, ApiResponse<UserResponseDto>>
{
    /// <summary>
    /// Reads the current profile from persistence rather than returning stale token metadata.
    /// </summary>
    /// <param name="request">The identity-bound query without consumer-supplied subject data.</param>
    /// <param name="cancellationToken">Cancels account reads.</param>
    /// <returns>The approved account projection in the shared envelope.</returns>
    public async ValueTask<ApiResponse<UserResponseDto>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        // Admin authorization requires a human subject before this handler can run.
        var result = await userService.GetAsync(authService.UserId!.Value, cancellationToken)
            ?? throw new BusinessException(AuthenticationErrorConstants.USER_MISSING,
                AuthenticationErrorConstants.ACCOUNT_UNAVAILABLE, BusinessErrorKind.NotFound);

        return new(result);
    }
}
