namespace ContextHub.Authentication.Application.Commands.Accounts.ResetPassword;

/// <summary>
/// Dispatches the ResetPassword form without accepting framework entities or logging its secret input.
/// </summary>
/// <param name="Request">The form input; protected identity comes from the authenticated recovery context.</param>
public sealed record ResetPasswordCommand(ResetPasswordRequestDto Request) : IRequest<PasswordResetOutcome>;
