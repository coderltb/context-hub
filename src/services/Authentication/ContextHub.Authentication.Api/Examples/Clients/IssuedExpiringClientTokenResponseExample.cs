namespace ContextHub.Authentication.Api.Examples.Clients;

/// <summary>
/// Illustrates an issued credential with the explicitly requested expiry.
/// </summary>
public sealed class IssuedExpiringClientTokenResponseExample : IOpenApiExampleProvider<ApiResponse<IssuedClientTokenResponseDto>>
{
    /// <summary>
    /// Creates a deterministic synthetic fixture using the endpoint's existing DTO contract.
    /// </summary>
    /// <returns>The documentation-only payload.</returns>
    public ApiResponse<IssuedClientTokenResponseDto> CreateExample() =>
        new(new IssuedClientTokenResponseDto(
            AuthenticationExampleConstants.CLIENT_ID,
            AuthenticationExampleConstants.TOKEN_ID,
            AuthenticationExampleConstants.TOKEN,
            AuthenticationConstants.BEARER_TOKEN_TYPE,
            AuthenticationExampleConstants.EXPIRES_AT)
        {
            AllowedTools = [AuthenticationExampleConstants.TOOL_NAME]
        })
        {
            Meta = ApiErrorExampleFactory.CreateMetadata(ApiValueConstants.API_VERSION)
        };
}
