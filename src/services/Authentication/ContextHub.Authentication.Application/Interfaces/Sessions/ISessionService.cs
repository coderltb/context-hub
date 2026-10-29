namespace ContextHub.Authentication.Application.Interfaces.Sessions;

/// <summary>
/// Exposes current-account and native authorization persistence without leaking Identity entities or protocol requests.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Loads the current active Owner rather than trusting previously issued role claims.
    /// </summary>
    /// <param name="subject">The authenticated subject, or null for an unusable native grant.</param>
    /// <param name="cancellationToken">Cancels the current-state query.</param>
    /// <returns>The approved account fields, or null when the grant is no longer permitted.</returns>
    Task<UserAccountModel?> GetPermittedUserAsync(string? subject, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the selected authenticated session against durable logout state without trusting a cached principal.
    /// </summary>
    /// <param name="subject">The protected cookie subject.</param>
    /// <param name="sessionId">The protected session binding.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>False for an unusable identity or a previously logged-out session.</returns>
    Task<bool> IsSessionActiveAsync(string? subject, string? sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists one authorization family and constructs only the claims approved for native issuance.
    /// </summary>
    /// <param name="user">The account already approved by the use case.</param>
    /// <param name="request">The trusted session and native-validated protocol metadata.</param>
    /// <param name="cancellationToken">Cancels authorization persistence.</param>
    /// <returns>The allowlisted principal, or null when logout has invalidated this session.</returns>
    Task<ClaimsPrincipal?> CreatePrincipalAsync(
        UserAccountModel user,
        AuthorizationGrantModel request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes only the authorization and token entries belonging to the specified SSO session.
    /// </summary>
    /// <param name="session">The authenticated subject and matching protected session identifier.</param>
    /// <param name="cancellationToken">Cancels revocation.</param>
    /// <returns>The session-family revocation operation.</returns>
    Task RevokeSessionAsync(SessionRevocationModel session, CancellationToken cancellationToken);
    /// <summary>
    /// Revokes all server-managed grants and tokens without deleting framework history.
    /// </summary>
    /// <param name="id">The human account identifier.</param>
    /// <param name="cancellationToken">Cancels lifecycle updates.</param>
    /// <returns>The revocation operation.</returns>
    Task RevokeAllAsync(Guid id, CancellationToken cancellationToken);
}
