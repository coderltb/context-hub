namespace ContextHub.Authentication.Infrastructure.Handlers.Authorization;

/// <summary>
/// Enforces the active Owner invariant for Authentication's own Admin endpoints.
/// </summary>
/// <param name="userRepository">The current-account repository.</param>
public sealed class CurrentOwnerHandler(IUserRepository userRepository)
    : AuthorizationHandler<CurrentOwnerRequirement>
{
    /// <summary>
    /// Grants the requirement only after reading current account state and Owner membership.
    /// </summary>
    /// <param name="context">The authenticated endpoint policy operation.</param>
    /// <param name="requirement">The requirement being evaluated.</param>
    /// <returns>The database-backed policy evaluation.</returns>
    protected override async Task HandleRequirementAsync(
        CurrentOwnerAuthorizationContext context,
        CurrentOwnerRequirement requirement)
    {
        var subject = context.User.GetClaim(Claims.Subject);

        // An old role claim cannot restore an account that is now inactive or no longer Owner.
        if (await userRepository.GetPermittedUserAsync(subject) is not null)
        {
            context.Succeed(requirement);
        }
    }
}
