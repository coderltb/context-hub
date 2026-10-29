namespace ContextHub.Authentication.Application.Policies;

/// <summary>
/// Applies the administration claim requirements after the host has validated the token and current Owner state.
/// </summary>
/// <typeparam name="TRequest">The protected administrative use case.</typeparam>
/// <param name="authService">The shared accessor for the authenticated principal.</param>
public sealed class AdminRequestAuthorizationPolicy<TRequest>(IAuthService authService) : IRequestAuthorizationPolicy<TRequest>
    where TRequest : IAdminRequest
{
    /// <summary>
    /// Rejects anonymous, application-token and insufficient-scope principals before validation or business work.
    /// </summary>
    /// <param name="request">The use case being authorized.</param>
    /// <param name="cancellationToken">Cancels request dispatch.</param>
    /// <returns>Whether the validated human principal satisfies the administration requirements.</returns>
    public ValueTask<bool> IsAllowedAsync(TRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The HTTP policy rechecks current Owner state; this policy also protects non-controller dispatch.
        return ValueTask.FromResult(authService.IsAuthenticated && authService.UserId is not null &&
            authService.Get<string>(AuthenticationConstants.ACTOR_TYPE) == AuthenticationConstants.USER_ACTOR &&
            authService.HasRole(AuthenticationConstants.OWNER) && authService.HasScope(AuthenticationConstants.ADMIN_SCOPE));
    }
}
