namespace ContextHub.Authentication.Application.Commands.Accounts.ResetPassword;

/// <summary>
/// Validates the form shape before native password policy, proof checks or provider IO.
/// </summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    /// <summary>
    /// Registers only transport-independent shape rules; native Identity owns credential validity.
    /// </summary>
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Request.Password)
            .NotEmpty();

        RuleFor(command => command.Request.ConfirmPassword)
            .Equal(command => command.Request.Password);
    }
}
