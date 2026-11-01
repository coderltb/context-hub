namespace ContextHub.Application.Policies;

/// <summary>
/// Reuses the validated principal to protect administration requests outside MVC as well.
/// </summary>
/// <typeparam name="TRequest">The protected administration request.</typeparam>
/// <param name="authService">The authenticated principal accessor.</param>
public sealed class AdminRequestAuthorizationPolicy<TRequest>(IAuthService authService)
    : IRequestAuthorizationPolicy<TRequest> where TRequest : IAdminRequest
{
    /// <summary>
    /// Requires the current human Owner and administration scope supplied by introspection.
    /// </summary>
    /// <param name="request">The administration operation.</param>
    /// <param name="cancellationToken">Cancels policy evaluation.</param>
    /// <returns>Whether the validated identity has the required grant.</returns>
    public ValueTask<bool> IsAllowedAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        // Introspection owns current account state; application credentials never gain Owner privileges.
        return ValueTask.FromResult(authService.IsAuthenticated && authService.UserId is not null &&
            authService.Get<string>(McpApplicationConstants.ACTOR_TYPE) == McpApplicationConstants.USER_ACTOR && authService.HasRole(McpApplicationConstants.OWNER_ROLE) &&
            authService.HasScope(McpApplicationConstants.ADMIN_SCOPE));
    }
}
