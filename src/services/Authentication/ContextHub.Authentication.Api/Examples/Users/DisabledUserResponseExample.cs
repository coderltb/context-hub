namespace ContextHub.Authentication.Api.Examples.Users;

/// <summary>
/// Illustrates the disabled account returned by the status-change example.
/// </summary>
public sealed class DisabledUserResponseExample : IOpenApiExampleProvider<ApiResponse<UserResponseDto>>
{
    /// <summary>
    /// Creates a deterministic synthetic fixture using the endpoint's existing DTO contract.
    /// </summary>
    /// <returns>The documentation-only payload.</returns>
    public ApiResponse<UserResponseDto> CreateExample() =>
        new(new UserResponseDto(
            AuthenticationExampleConstants.USER_ID,
            AuthenticationExampleConstants.USER_NAME,
            AuthenticationExampleConstants.EMAIL,
            false,
            [AuthenticationConstants.OWNER],
            AuthenticationExampleConstants.CREATED_AT,
            AuthenticationExampleConstants.CREATED_AT,
            AuthenticationExampleConstants.CONCURRENCY_TOKEN))
        {
            Meta = ApiErrorExampleFactory.CreateMetadata(ApiValueConstants.API_VERSION)
        };
}
