namespace ContextHub.Authentication.Application.Commands.Sessions.RevokeSession;

/// <summary>
/// Orchestrates current-session revocation without changing unrelated devices or application credentials.
/// </summary>
/// <param name="sessionService">The native authorization-family persistence boundary.</param>
/// <param name="authService">The authenticated cookie accessor.</param>
public sealed class RevokeSessionCommandHandler(
    ISessionService sessionService,
    IAuthService authService) : IRequestHandler<RevokeSessionCommand, Unit>
{
    /// <summary>
    /// Revokes the selected family before the HTTP adapter clears its local cookie.
    /// </summary>
    /// <param name="request">The use-case marker; subject and session come from the selected cookie accessor.</param>
    /// <param name="cancellationToken">Cancels native revocation work.</param>
    /// <returns>The completed command marker without a REST envelope.</returns>
    public async ValueTask<Unit> Handle(
        RevokeSessionCommand request,
        CancellationToken cancellationToken)
    {
        var subject = authService.Get<string>(ClaimTypes.NameIdentifier);
        var sessionId = authService.Get<string>(AuthenticationConstants.SESSION_ID);

        // Anonymous logout still clears the cookie; it must not revoke a family taken from another identity source.
        if (authService.IsAuthenticated && !string.IsNullOrEmpty(subject) && !string.IsNullOrEmpty(sessionId))
        {
            await sessionService.RevokeSessionAsync(new SessionRevocationModel(subject, sessionId), cancellationToken);
        }

        return Unit.Value;
    }
}
