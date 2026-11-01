namespace ContextHub.Authentication.Application.Queries.Sessions.ValidateTokenGrant;

/// <summary>
/// Prevents an old code or refresh grant from restoring a disabled or no-longer-permitted Owner.
/// </summary>
/// <param name="sessionService">The service port for current account eligibility.</param>
/// <param name="authService">The existing accessor for the selected native code or refresh principal.</param>
/// <param name="logger">The outcome logger without protocol credentials or principal payloads.</param>
public sealed class ValidateTokenGrantQueryHandler(
    ISessionService sessionService,
    IAuthService authService,
    ILogger<ValidateTokenGrantQueryHandler> logger) : IRequestHandler<ValidateTokenGrantQuery, bool>
{
    /// <summary>
    /// Checks present authority while leaving redemption, rotation and token persistence with OpenIddict.
    /// </summary>
    /// <param name="request">The use-case marker; the selected subject is read from the authenticated accessor.</param>
    /// <param name="cancellationToken">Cancels the current-account query.</param>
    /// <returns>Whether native issuance may continue.</returns>
    public async ValueTask<bool> Handle(
        ValidateTokenGrantQuery request,
        CancellationToken cancellationToken)
    {
        var subject = authService.Get<string>(CoreConstants.AuthenticationVariables.SUBJECT_CLAIM);

        // Only the filter-selected protocol principal is authoritative for this token exchange.
        var allowed = authService.IsAuthenticated && !string.IsNullOrEmpty(subject) &&
            await sessionService.GetPermittedUserAsync(subject, cancellationToken) is not null;
        logger.LogInformation(AuthenticationLogConstants.GRANT_VALIDATED, allowed);

        return allowed;
    }
}
