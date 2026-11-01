namespace ContextHub.Authentication.Infrastructure.Services.Sessions;

/// <summary>
/// Builds Admin grants from a password-authenticated Owner cookie and manages their native authorization family.
/// </summary>
/// <param name="userRepository">The feature account query repository.</param>
/// <param name="applicationManager">The native protocol client manager.</param>
/// <param name="authorizationManager">The authorization lifecycle manager.</param>
/// <param name="tokenManager">The token lifecycle manager.</param>
/// <param name="unitOfWork">The scoped transaction boundary making logout atomic across native store writes.</param>
/// <param name="userManager">The native Identity store writing per-session logout markers.</param>
/// <param name="logger">The protocol operation logger without credential or client-secret data.</param>
public sealed class SessionService(
    IUserRepository userRepository,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictTokenManager tokenManager,
    IUnitOfWork unitOfWork,
    UserManager<User> userManager,
    ILogger<SessionService> logger) : ISessionService
{
    /// <summary>
    /// Rechecks current account state before an Admin grant may be issued or refreshed.
    /// </summary>
    /// <param name="subject">The already authenticated subject identifier.</param>
    /// <param name="cancellationToken">Cancels the current-account query.</param>
    /// <returns>The permitted user, or null for a rejected grant.</returns>
    public Task<UserAccountModel?> GetPermittedUserAsync(string? subject, CancellationToken cancellationToken) =>
        userRepository.GetPermittedUserAsync(subject, cancellationToken);

    /// <summary>
    /// Checks durable logout state while preserving unrelated browser sessions.
    /// </summary>
    /// <param name="subject">The protected subject from the selected cookie.</param>
    /// <param name="sessionId">The protected browser-session binding.</param>
    /// <param name="cancellationToken">Cancels the authoritative database read.</param>
    /// <returns>Whether this authenticated session has not been logged out.</returns>
    public async Task<bool> IsSessionActiveAsync(
        string? subject,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subject, out var userId) || string.IsNullOrEmpty(sessionId))
        {
            return false;
        }

        return !await userRepository.IsSessionRevokedAsync(userId, sessionId, cancellationToken);
    }

    /// <summary>
    /// Creates a distinct authorization family bound to the current SSO session instead of sharing all devices' grants.
    /// </summary>
    /// <param name="user">The currently permitted Owner.</param>
    /// <param name="request">The trusted session and native-validated protocol metadata.</param>
    /// <param name="cancellationToken">Cancels authorization persistence.</param>
    /// <returns>The approved principal, or null for a session already ended by logout.</returns>
    public async Task<ClaimsPrincipal?> CreatePrincipalAsync(
        UserAccountModel user,
        AuthorizationGrantModel request,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync<ClaimsPrincipal?>(
            async token =>
        {
            // Serialize the marker read with logout so an overlapping request cannot restore a revoked session.
            if (!await IsSessionActiveAsync(user.Id.ToString(), request.SessionId, token))
            {
                return null;
            }

            // Build claims only from approved account and grant fields.
            var identity = new ClaimsIdentity(AuthenticationConstants.BEARER_TOKEN_TYPE, Claims.Name, Claims.Role);
            var principal = new ClaimsPrincipal(identity);
            principal.SetClaim(Claims.Subject, user.Id.ToString());
            principal.SetClaim(Claims.Name, user.UserName);
            principal.SetClaim(Claims.Role, AuthenticationConstants.OWNER);
            principal.SetClaim(AuthenticationConstants.ACTOR_TYPE, AuthenticationConstants.USER_ACTOR);
            principal.SetClaim(AuthenticationConstants.SESSION_ID, request.SessionId);
            principal.SetScopes(request.Scopes);
            principal.SetResources(AuthenticationConstants.RESOURCE);

            // Assign destinations only to protocol-approved public claims.
            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(Destinations.AccessToken, Destinations.IdentityToken);
            }

            // Bind each cookie session to its own authorization family.
            var application = await applicationManager.FindByClientIdAsync(request.ClientId, token);

            if (application is null)
            {
                logger.LogError(
                    AuthenticationLogConstants.OPERATION_REJECTED,
                    nameof(CreatePrincipalAsync),
                    AuthenticationErrorConstants.PROTOCOL_CLIENT_UNAVAILABLE);
                throw new InvalidOperationException(AuthenticationErrorConstants.PROTOCOL_CLIENT_UNAVAILABLE);
            }

            // Store session metadata for scoped grant revocation.
            var descriptor = new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = await applicationManager.GetIdAsync(application, token),
                Subject = user.Id.ToString(),
                Status = Statuses.Valid,
                Type = AuthorizationTypes.Permanent,
                Principal = principal
            };

            descriptor.Scopes.UnionWith(request.Scopes);
            descriptor.Properties[AuthenticationConstants.SESSION_ID] = JsonSerializer.SerializeToElement(request.SessionId);
            var authorization = await authorizationManager.CreateAsync(descriptor, token);

            // Bind issued tokens to this revocable authorization family.
            principal.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization, token));

            return principal;
        },
            IsolationLevel.Serializable,
            cancellationToken);
    }

    /// <summary>
    /// Revokes only authorization families belonging to the signed-in SSO session.
    /// </summary>
    /// <param name="session">The authenticated cookie subject and protected session identifier.</param>
    /// <param name="cancellationToken">Cancels revocation.</param>
    /// <returns>The session revocation operation.</returns>
    public async Task RevokeSessionAsync(
        SessionRevocationModel session,
        CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
        {
            // Keep logout durable even when the cookie has never created an OpenIddict family.
            var user = await userManager.FindByIdAsync(session.Subject);
            if (user is not null && !(await userManager.SetAuthenticationTokenAsync(
                user,
                AuthenticationInfrastructureConstants.SSO_REVOCATION_PROVIDER,
                session.SessionId,
                string.Empty)).Succeeded)
            {
                logger.LogWarning(
                    AuthenticationLogConstants.OPERATION_REJECTED,
                    nameof(RevokeSessionAsync),
                    AuthenticationErrorConstants.GRANT_REVOCATION_FAILED);
                throw new InvalidOperationException(AuthenticationErrorConstants.GRANT_REVOCATION_FAILED);
            }

            // Fully consume the native EF stream before any update or nested query uses this scoped connection.
            var authorizations = await authorizationManager.FindBySubjectAsync(session.Subject, token).ToListAsync(token);
            foreach (var authorization in authorizations)
            {
                var properties = await authorizationManager.GetPropertiesAsync(authorization, token);

                // A cookie session owns only its matching families; other devices and independently issued client tokens remain separate.
                if (!properties.TryGetValue(AuthenticationConstants.SESSION_ID, out var stored) || stored.GetString() != session.SessionId)
                {
                    continue;
                }

                // Native TryRevoke reports store failure as false. Do not clear the cookie or commit a partial logout.
                if (!await authorizationManager.TryRevokeAsync(authorization, token))
                {
                    token.ThrowIfCancellationRequested();
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        nameof(RevokeSessionAsync),
                        AuthenticationErrorConstants.GRANT_REVOCATION_FAILED);
                    throw new InvalidOperationException(AuthenticationErrorConstants.GRANT_REVOCATION_FAILED);
                }

                var authorizationId = await authorizationManager.GetIdAsync(authorization, token);
                await tokenManager.RevokeByAuthorizationIdAsync(authorizationId!, token);
            }
        },
            IsolationLevel.Serializable,
            cancellationToken);

        // The controller clears the cookie only after this committed security operation returns successfully.
        logger.LogInformation(AuthenticationLogConstants.OPERATION_COMPLETED, nameof(RevokeSessionAsync));
    }

    /// <summary>
    /// Revokes all server-managed grants and tokens without deleting framework history.
    /// </summary>
    /// <param name="id">The human account identifier.</param>
    /// <param name="cancellationToken">Cancels lifecycle updates.</param>
    /// <returns>The revocation operation.</returns>
    public async Task RevokeAllAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Native bulk operations share the caller's UoW transaction and propagate provider errors and cancellation.
        var subject = id.ToString();
        await authorizationManager.RevokeBySubjectAsync(subject, cancellationToken);
        await tokenManager.RevokeBySubjectAsync(subject, cancellationToken);
    }
}
