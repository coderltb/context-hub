namespace ContextHub.Authentication.Application.Queries.Users.GetUser;

/// <summary>
/// Orchestrates the authorized account operation through the Application-owned port.
/// </summary>
/// <param name="userService">The account boundary implemented using native Identity managers.</param>
public sealed class GetUserQueryHandler(IUserService userService)
    : IRequestHandler<GetUserQuery, ApiResponse<UserResponseDto>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels account reads or writes.</param>
    /// <returns>The approved account response.</returns>
    public async ValueTask<ApiResponse<UserResponseDto>> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken)
    {
        // The service returns an allowlisted projection, never the Identity entity or credential fields.
        var result = await userService.GetAsync(request.UserId, cancellationToken)
            ?? throw new BusinessException(AuthenticationErrorConstants.USER_MISSING,
                AuthenticationErrorConstants.ACCOUNT_UNAVAILABLE, BusinessErrorKind.NotFound);

        return new(result);
    }
}
