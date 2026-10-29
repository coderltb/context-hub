namespace ContextHub.Authentication.Api.Enums;

/// <summary>
/// Selects the native identity source for one OIDC action; an uninitialized operation is rejected.
/// </summary>
public enum OidcAuthenticationOperation
{
    /// <summary>
    /// Rejects an unspecified operation.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Authenticates the Identity cookie before an authorization grant.
    /// </summary>
    Authorize = 1,

    /// <summary>
    /// Authenticates the protocol code or refresh token.
    /// </summary>
    Exchange = 2,

    /// <summary>
    /// Optionally authenticates the Identity cookie before current-session revocation.
    /// </summary>
    Logout = 3
}
