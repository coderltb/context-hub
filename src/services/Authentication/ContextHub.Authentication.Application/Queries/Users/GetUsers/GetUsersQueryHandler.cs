namespace ContextHub.Authentication.Application.Queries.Users.GetUsers;

/// <summary>
/// Orchestrates the authorized account operation through the Application-owned port.
/// </summary>
/// <param name="userService">The account boundary implemented using native Identity managers.</param>
public sealed class GetUsersQueryHandler(IUserService userService)
    : IRequestHandler<GetUsersQuery, ApiResponse<PageResponse<UserResponseDto>>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels account reads or writes.</param>
    /// <returns>The approved account response.</returns>
    public async ValueTask<ApiResponse<PageResponse<UserResponseDto>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        // The specific repository applies the grouped search and ordering before materializing this page.
        var result = await userService.GetPageAsync(request.Request, cancellationToken);

        return new(result);
    }
}
