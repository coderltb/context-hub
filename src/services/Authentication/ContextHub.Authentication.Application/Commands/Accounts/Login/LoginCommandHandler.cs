namespace ContextHub.Authentication.Application.Commands.Accounts.Login;

/// <summary>
/// Keeps form orchestration in Application while native Identity owns credential verification.
/// </summary>
/// <param name="accountService">The native account port without framework types.</param>
public sealed class LoginCommandHandler(IAccountService accountService) : IRequestHandler<LoginCommand, bool>
{
    /// <summary>
    /// Delegates verified form input without logging its secret fields.
    /// </summary>
    /// <param name="request">The submitted SSO proof.</param>
    /// <param name="cancellationToken">Stops dispatch before native credential handling.</param>
    /// <returns>Whether the account operation succeeded.</returns>
    public async ValueTask<bool> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Native Identity login APIs do not accept cancellation.
        cancellationToken.ThrowIfCancellationRequested();

        return await accountService.LoginAsync(request.Request);
    }
}
