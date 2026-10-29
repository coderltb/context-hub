namespace ContextHub.Authentication.Application.Commands.Accounts.AcceptInvitation;

/// <summary>
/// Keeps form orchestration in Application while native Identity owns credential verification.
/// </summary>
/// <param name="accountService">The native account port without framework types.</param>
public sealed class AcceptInvitationCommandHandler(IAccountService accountService) : IRequestHandler<AcceptInvitationCommand, bool>
{
    /// <summary>
    /// Delegates verified form input without logging its secret fields.
    /// </summary>
    /// <param name="request">The submitted SSO proof.</param>
    /// <param name="cancellationToken">Cancels the invitation account query and persistence.</param>
    /// <returns>Whether the account operation succeeded.</returns>
    public async ValueTask<bool> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        // Passwords and invitation proofs remain inside the native account service.
        return await accountService.AcceptInvitationAsync(request.Request, cancellationToken);
    }
}
