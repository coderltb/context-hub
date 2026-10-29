namespace ContextHub.Authentication.Infrastructure.Services.Accounts;

/// <summary>
/// Keeps interactive credential handling in Identity while Razor adapters bind forms and redirects.
/// </summary>
/// <param name="userManager">The native credential manager.</param>
/// <param name="signInManager">The native password-check and cookie manager.</param>
/// <param name="userRepository">The feature repository using the same scoped context as native Identity stores.</param>
/// <param name="logger">The outcome logger that never receives submitted credentials.</param>
public sealed class AccountService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IUserRepository userRepository,
    ILogger<AccountService> logger) : IAccountService
{
    /// <summary>
    /// Checks credentials with native lockout before establishing the Owner password session.
    /// </summary>
    /// <param name="request">The credentials consumed only by native Identity; never log this DTO.</param>
    /// <returns>Whether verification established the password session.</returns>
    public async Task<bool> LoginAsync(LoginRequestDto request)
    {
        // Let Identity normalize the login name and use its native account store before credential verification.
        var user = await userManager.FindByNameAsync(request.UserName);

        // Keep unavailable accounts indistinguishable at this boundary; only Identity verifies password and lockout state.
        if (user is null || !user.IsActive || !await userManager.IsInRoleAsync(user, AuthenticationConstants.OWNER))
        {
            logger.LogWarning(AuthenticationLogConstants.OPERATION_REJECTED, nameof(LoginAsync), AuthenticationErrorConstants.SIGN_IN_FAILED);

            return false;
        }

        // Identity retains password policy, lockout and concurrency checks; this product uses password-only interactive sign-in.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Reset legacy failure counts before issuing the password-only cookie.
            if (user.AccessFailedCount > 0 && !(await userManager.ResetAccessFailedCountAsync(user)).Succeeded)
            {
                return false;
            }

            // Issue one cookie with a fresh session binding; authorization separately rechecks the active Owner membership.
            await EstablishCookieAsync(user);
        }

        return result.Succeeded;
    }

    /// <summary>
    /// Accepts an invitation through native password and concurrency handling; no separate password or SQL engine is needed.
    /// </summary>
    /// <param name="request">The invitation proof and initial password entered only at SSO.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>Whether setup succeeded; every rejected invitation has the same public outcome.</returns>
    public async Task<bool> AcceptInvitationAsync(
        AcceptInvitationRequestDto request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FirstOrDefaultAsync(
            predicate: value => value.Id == request.UserId,
            cancellationToken: cancellationToken);

        // Accept only an active passwordless account with proof from the invitation provider and purpose.
        if (user is null || !user.IsActive || await userManager.HasPasswordAsync(user) ||
            !await userManager.VerifyUserTokenAsync(
                user,
                AuthenticationConstants.INVITATION_PROVIDER,
                AuthenticationConstants.INVITATION_PURPOSE,
                request.Token))
        {
            return false;
        }

        // Set the first password through Identity; native policy and concurrency checks decide whether setup succeeds.
        var result = await userManager.AddPasswordAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return false;
        }

        // Native password persistence invalidates the invitation before cookie issuance.
        await EstablishCookieAsync(user);

        return true;
    }

    /// <summary>
    /// Establishes one protected password session after successful native credential verification.
    /// </summary>
    /// <param name="user">The verified active Owner.</param>
    /// <returns>The native cookie sign-in operation.</returns>
    private Task EstablishCookieAsync(User user) => signInManager.SignInWithClaimsAsync(
        user,
        false,
        [
            new Claim(AuthenticationConstants.SESSION_ID, Guid.NewGuid().ToString("N")),
            new Claim(Claims.AuthenticationMethodReference, AuthenticationConstants.PASSWORD_AUTHENTICATION_METHOD)
        ]);
}
