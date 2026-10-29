namespace ContextHub.Authentication.Application.Commands.Accounts.ForgotPassword;

/// <summary>
/// Validates the form shape before native password policy, proof checks or provider IO.
/// </summary>
public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    /// <summary>
    /// Registers only transport-independent shape rules; native Identity owns credential validity.
    /// </summary>
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH);
    }
}
