namespace ContextHub.Authentication.Infrastructure.Handlers.Introspection;

/// <summary>
/// Rechecks human account access before introspection exposes an active Admin identity.
/// </summary>
/// <param name="userRepository">The current-account repository backed by the same scoped context.</param>
public sealed class CurrentUserIntrospectionHandler(IUserRepository userRepository)
    : IOpenIddictServerHandler<HandleIntrospectionRequestContext>
{
    /// <summary>
    /// Refuses stale human rights even when a previously issued access token has not expired.
    /// </summary>
    /// <param name="context">The authenticated backchannel request.</param>
    /// <returns>The asynchronous policy verification.</returns>
    public async ValueTask HandleAsync(HandleIntrospectionRequestContext context)
    {
        // Native validation has populated this principal before the introspection handler runs.
        var principal = context.GenericTokenPrincipal;

        // Client credentials were checked by their own store; they are not Identity users or Admin sessions.
        if (principal.GetClaim(AuthenticationConstants.ACTOR_TYPE) == AuthenticationConstants.CLIENT_ACTOR)
        {
            return;
        }

        var subject = principal.GetClaim(Claims.Subject);

        if (await userRepository.GetPermittedUserAsync(subject, context.CancellationToken) is null)
        {
            context.Reject(Errors.InvalidToken);
        }
    }
}
