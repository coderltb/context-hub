namespace ContextHub.Authentication.Application.Commands.Accounts.AcceptInvitation;

/// <summary>
/// Dispatches the anonymous SSO form through the Application account boundary.
/// </summary>
/// <param name="Request">The submitted proof; never include it in diagnostics.</param>
public sealed record AcceptInvitationCommand(AcceptInvitationRequestDto Request) : IRequest<bool>;
