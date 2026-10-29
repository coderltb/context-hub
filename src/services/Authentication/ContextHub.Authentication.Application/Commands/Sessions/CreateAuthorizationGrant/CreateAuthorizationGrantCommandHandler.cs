namespace ContextHub.Authentication.Application.Commands.Sessions.CreateAuthorizationGrant;

/// <summary>
/// Decides current Owner eligibility from the selected cookie while leaving protocol issuance native.
/// </summary>
/// <param name="sessionService">The current-account and authorization service port.</param>
/// <param name="authService">The existing accessor for the principal selected by native authentication.</param>
/// <param name="logger">The safe use-case outcome logger.</param>
public sealed class CreateAuthorizationGrantCommandHandler(
    ISessionService sessionService,
    IAuthService authService,
    ILogger<CreateAuthorizationGrantCommandHandler> logger)
    : IRequestHandler<CreateAuthorizationGrantCommand, AuthorizationGrantResultModel>
{
    /// <summary>
    /// Binds protocol metadata to the authenticated cookie before authorization persistence.
    /// </summary>
    /// <param name="request">The native adapter's protocol metadata.</param>
    /// <param name="cancellationToken">Cancels eligibility checks and grant persistence.</param>
    /// <returns>The native sign-in, rejection or approved-principal decision.</returns>
    public async ValueTask<AuthorizationGrantResultModel> Handle(
        CreateAuthorizationGrantCommand request,
        CancellationToken cancellationToken)
    {
        var subject = authService.Get<string>(ClaimTypes.NameIdentifier);

        // Identity cookies and protocol tokens have different subject mappings; do not fall back between sources.
        if (!authService.IsAuthenticated || string.IsNullOrEmpty(subject))
        {
            logger.LogInformation(
                AuthenticationLogConstants.GRANT_DECIDED,
                nameof(CreateAuthorizationGrantCommand),
                AuthorizationGrantDecision.RequiresSignIn);

            return new(AuthorizationGrantDecision.RequiresSignIn);
        }

        var user = await sessionService.GetPermittedUserAsync(subject, cancellationToken);
        var sessionId = authService.Get<string>(AuthenticationConstants.SESSION_ID);
        var grant = request.Request;

        // A formerly permitted cookie cannot restore a disabled Owner or create an unbound authorization family.
        if (user is null || !grant.ProtocolRequirementsSatisfied || string.IsNullOrEmpty(sessionId) ||
            string.IsNullOrEmpty(grant.ClientId))
        {
            logger.LogInformation(
                AuthenticationLogConstants.GRANT_DECIDED,
                nameof(CreateAuthorizationGrantCommand),
                AuthorizationGrantDecision.Rejected);

            return new(AuthorizationGrantDecision.Rejected);
        }

        var principal = await sessionService.CreatePrincipalAsync(
            user,
            new AuthorizationGrantModel(sessionId, grant.ClientId, grant.Scopes),
            cancellationToken);
        var decision = principal is null ? AuthorizationGrantDecision.Rejected : AuthorizationGrantDecision.Granted;
        logger.LogInformation(AuthenticationLogConstants.GRANT_DECIDED, nameof(CreateAuthorizationGrantCommand), decision);

        return new(decision, principal);
    }

}
