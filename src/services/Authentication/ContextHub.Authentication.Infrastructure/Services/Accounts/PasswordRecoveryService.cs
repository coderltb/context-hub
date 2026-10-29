namespace ContextHub.Authentication.Infrastructure.Services.Accounts;

/// <summary>
/// Keeps native Identity proof, Redis budgets and atomic password/grant persistence behind the recovery port.
/// </summary>
/// <param name="userManager">The native password and token manager.</param>
/// <param name="userRepository">The specific entity-query boundary using the same scoped context.</param>
/// <param name="atomicCacheService">The Redis-backed fixed-window coordination operations without a memory fallback.</param>
/// <param name="emailService">The transactional provider adapter.</param>
/// <param name="sessionService">The existing human-grant lifecycle service.</param>
/// <param name="unitOfWork">The same scoped transaction boundary used by Identity and OpenIddict stores.</param>
/// <param name="logger">The safe security-event logger.</param>
public sealed class PasswordRecoveryService(
    UserManager<User> userManager,
    IUserRepository userRepository,
    IAtomicCacheService atomicCacheService,
    IEmailService emailService,
    ISessionService sessionService,
    IUnitOfWork unitOfWork,
    ILogger<PasswordRecoveryService> logger) : IPasswordRecoveryService
{
    /// <summary>
    /// Reserves a same-address cooldown and hourly budget before Application performs account-dependent work.
    /// </summary>
    /// <param name="request">The submitted recovery address.</param>
    /// <param name="cancellationToken">Cancels Redis commands before dispatch where supported.</param>
    /// <returns>Whether another send may be attempted.</returns>
    public async Task<bool> ReserveRequestAsync(
        ForgotPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        // Use one normalized, hashed cache identity across browsers; neither the address nor OTP becomes a plaintext cache key.
        var normalized = userManager.NormalizeEmail(request.Email.Trim());
        var cooldown = CacheKey.Create(AuthenticationRecoveryConstants.CACHE_NAMESPACE,
            AuthenticationRecoveryConstants.KEY_VERSION, AuthenticationRecoveryConstants.COOLDOWN_KEY, normalized);

        // Reserve the resend cooldown atomically before account lookup or email transport work.
        if (!await atomicCacheService.TrySetIfAbsentAsync(cooldown.Value, AuthenticationRecoveryConstants.RESERVATION_VALUE,
            TimeSpan.FromSeconds(AuthenticationRecoveryConstants.SEND_COOLDOWN_SECONDS), cancellationToken))
        {
            return false;
        }

        // Neither resend nor verification resets these counters; different browsers share the same hashed identity key.
        var hourly = CacheKey.Create(AuthenticationRecoveryConstants.CACHE_NAMESPACE,
            AuthenticationRecoveryConstants.KEY_VERSION, AuthenticationRecoveryConstants.SEND_BUDGET_KEY, normalized);
        var sends = await atomicCacheService.IncrementAsync(hourly.Value, TimeSpan.FromHours(1), cancellationToken);

        return sends <= AuthenticationRecoveryConstants.MAX_SENDS_PER_HOUR;
    }

    /// <summary>
    /// Reads the current eligible Owner without relying on cached authorization or exposing an Identity entity.
    /// </summary>
    /// <param name="emailAddress">The submitted address normalized by the native manager.</param>
    /// <param name="cancellationToken">Cancels the entity query.</param>
    /// <returns>Approved recovery metadata, or null for every unavailable account.</returns>
    public async Task<PasswordRecoveryAccountModel?> GetAccountAsync(
        string emailAddress,
        CancellationToken cancellationToken)
    {
        var normalized = userManager.NormalizeEmail(emailAddress.Trim());

        // Legacy duplicates, including inactive accounts, must never select an arbitrary recovery identity.
        if (await userRepository.CountAsync(value => value.NormalizedEmail == normalized,
            cancellationToken: cancellationToken) != 1)
        {
            return null;
        }

        var user = await userRepository.FirstOrDefaultAsync(
            predicate: value => value.NormalizedEmail == normalized && value.IsActive,
            cancellationToken: cancellationToken);

        // Passwordless invitations and client identities cannot become password-reset sessions.
        if (user?.Email is null || !await userManager.HasPasswordAsync(user) ||
            !await userManager.IsInRoleAsync(user, AuthenticationConstants.OWNER))
        {
            return null;
        }

        return new(user.Id, user.Email);
    }

    /// <summary>
    /// Uses the configured native email provider for numeric proof rather than implementing an OTP generator or store.
    /// </summary>
    /// <param name="account">The approved account and destination.</param>
    /// <param name="cancellationToken">Cancels the fresh entity query and SDK send.</param>
    /// <returns>Whether the provider accepted the message.</returns>
    public async Task<bool> SendOtpAsync(
        PasswordRecoveryAccountModel account,
        CancellationToken cancellationToken)
    {
        // Recheck the active account immediately before generating proof; an earlier lookup may no longer describe its current state.
        var user = await userRepository.FirstOrDefaultAsync(
            predicate: value => value.Id == account.UserId && value.IsActive,
            cancellationToken: cancellationToken);
        if (user is null)
        {
            return false;
        }

        // Native email tokens use their own time window; invitation DataProtection lifetime does not govern this code.
        var code = await userManager.GeneratePasswordResetTokenAsync(user);

        // Build the transport payload from native proof; this DTO and its body must never enter request/response diagnostics.
        var message = new EmailRequestDto(account.Email, AuthenticationRecoveryConstants.EMAIL_SUBJECT,
            string.Format(AuthenticationRecoveryConstants.EMAIL_TEXT, code),
            string.Format(AuthenticationRecoveryConstants.EMAIL_HTML, code));

        return await emailService.SendAsync(message, cancellationToken);
    }

    /// <summary>
    /// Applies the shared attempt limit before checking the same native provider and reset purpose used at issuance.
    /// </summary>
    /// <param name="request">The submitted recovery address and six-digit code.</param>
    /// <param name="cancellationToken">Cancels coordination and account queries.</param>
    /// <returns>Metadata for a restricted cookie, or null without account-specific diagnostics.</returns>
    public async Task<PasswordRecoveryGrantModel?> VerifyAsync(
        VerifyPasswordResetOtpRequestDto request,
        CancellationToken cancellationToken)
    {
        var normalized = userManager.NormalizeEmail(request.Email.Trim());
        var key = CacheKey.Create(AuthenticationRecoveryConstants.CACHE_NAMESPACE,
            AuthenticationRecoveryConstants.KEY_VERSION, AuthenticationRecoveryConstants.VERIFY_BUDGET_KEY, normalized);

        // Count every verification attempt before account and OTP lookup, so changing browsers or resending cannot reset the shared budget.
        var attempts = await atomicCacheService.IncrementAsync(key.Value,
            TimeSpan.FromMinutes(AuthenticationRecoveryConstants.VERIFICATION_WINDOW_MINUTES), cancellationToken);
        if (attempts > AuthenticationRecoveryConstants.MAX_VERIFICATION_ATTEMPTS)
        {
            return null;
        }

        // Resolve only an eligible active Owner with a password; missing and ineligible accounts share the same safe rejection.
        var account = await GetAccountAsync(request.Email, cancellationToken);
        if (account is null)
        {
            return null;
        }

        // Validate the proof with the configured native provider and reset purpose, not a custom OTP comparison.
        var user = await userRepository.FirstOrDefaultAsync(
            predicate: value => value.Id == account.UserId && value.IsActive,
            cancellationToken: cancellationToken);
        if (user is null || !await userManager.VerifyUserTokenAsync(user,
            userManager.Options.Tokens.PasswordResetTokenProvider, UserManager<User>.ResetPasswordTokenPurpose, request.OtpCode))
        {
            return null;
        }

        // Verification does not consume the native OTP; successful password reset changes its security-stamp binding.
        return new(user.Id, request.OtpCode);
    }

    /// <summary>
    /// Commits password change and human-grant revocation together without issuing a login cookie or changing client credentials.
    /// </summary>
    /// <param name="request">The proof from the authenticated recovery principal and the replacement password.</param>
    /// <param name="cancellationToken">Cancels the owned database transaction.</param>
    /// <returns>The safe form outcome after native persistence has committed or rolled back.</returns>
    public async Task<PasswordResetOutcome> ResetAsync(
        PasswordResetRequestModel request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Keep password reset and grant revocation atomic; any native mutation failure must roll back before returning a form outcome.
            var outcome = await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                // Recheck current account eligibility inside the write transaction, even when the protected recovery cookie was valid earlier.
                var user = await userRepository.FirstOrDefaultAsync(
                    predicate: value => value.Id == request.UserId && value.IsActive,
                    cancellationToken: token);
                if (user is null || !await userManager.HasPasswordAsync(user) ||
                    !await userManager.IsInRoleAsync(user, AuthenticationConstants.OWNER))
                {
                    return PasswordResetOutcome.Rejected;
                }

                // Let Identity revalidate the proof, enforce password policy and update the password hash and stamp.
                var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password);
                if (!result.Succeeded)
                {
                    var policyRejected = result.Errors.All(error => error.Code.StartsWith(
                        AuthenticationRecoveryConstants.NATIVE_PASSWORD_ERROR_PREFIX, StringComparison.Ordinal));
                    var code = policyRejected ? AuthenticationErrorConstants.PASSWORD_POLICY_REJECTED
                        : AuthenticationErrorConstants.RESET_REJECTED;
                    logger.LogWarning(AuthenticationLogConstants.OPERATION_REJECTED, nameof(ResetAsync), code);

                    // A failed native update may leave tracked edits. Roll back before returning a safe form outcome.
                    throw new BusinessException(code, AuthenticationErrorConstants.INVALID_PROOF_MESSAGE);
                }

                // ResetPassword already updates the native security stamp; do not save another redundant stamp change.
                await sessionService.RevokeAllAsync(user.Id, token);

                return PasswordResetOutcome.Succeeded;
            }, cancellationToken);

            // Record completion only after the transaction commits; the outcome never includes password or OTP material.
            if (outcome == PasswordResetOutcome.Succeeded)
            {
                logger.LogInformation(AuthenticationLogConstants.RECOVERY_COMPLETED);
            }

            return outcome;
        }
        catch (BusinessException exception) when (exception.ErrorCode is
            AuthenticationErrorConstants.PASSWORD_POLICY_REJECTED or AuthenticationErrorConstants.RESET_REJECTED)
        {
            // Only these expected native rejections become form outcomes, after the UoW has rolled back.
            return exception.ErrorCode == AuthenticationErrorConstants.PASSWORD_POLICY_REJECTED
                ? PasswordResetOutcome.PasswordRejected : PasswordResetOutcome.Rejected;
        }
    }
}
