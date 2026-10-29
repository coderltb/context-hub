namespace ContextHub.Authentication.Application.Commands.Accounts.VerifyPasswordResetOtp;

/// <summary>
/// Validates the form shape before native password policy, proof checks or provider IO.
/// </summary>
public sealed class VerifyPasswordResetOtpCommandValidator : AbstractValidator<VerifyPasswordResetOtpCommand>
{
    /// <summary>
    /// Registers only transport-independent shape rules; native Identity owns credential validity.
    /// </summary>
    public VerifyPasswordResetOtpCommandValidator()
    {
        RuleFor(command => command.Request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH);

        RuleFor(command => command.Request.OtpCode)
            .NotEmpty()
            .Matches(AuthenticationRecoveryConstants.OTP_PATTERN);
    }
}
