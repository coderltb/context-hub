namespace ContextHub.Authentication.Application.Commands.Accounts.VerifyPasswordResetOtp;

/// <summary>
/// Dispatches the VerifyPasswordResetOtp form without accepting framework entities or logging its secret input.
/// </summary>
/// <param name="Request">The form input; protected identity comes from the authenticated recovery context.</param>
public sealed record VerifyPasswordResetOtpCommand(VerifyPasswordResetOtpRequestDto Request) : IRequest<PasswordRecoveryGrantModel?>;
