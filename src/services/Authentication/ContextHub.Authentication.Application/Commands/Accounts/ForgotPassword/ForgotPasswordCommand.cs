namespace ContextHub.Authentication.Application.Commands.Accounts.ForgotPassword;

/// <summary>
/// Dispatches the ForgotPassword form without accepting framework entities or logging its secret input.
/// </summary>
/// <param name="Request">The form input; protected identity comes from the authenticated recovery context.</param>
public sealed record ForgotPasswordCommand(ForgotPasswordRequestDto Request) : IRequest<bool>;
