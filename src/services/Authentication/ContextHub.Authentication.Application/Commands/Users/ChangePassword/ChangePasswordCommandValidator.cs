namespace ContextHub.Authentication.Application.Commands.Users.ChangePassword;

/// <summary>
/// Requires both credentials while leaving password complexity and verification to native Identity.
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>
    /// Configures the shared validation pipeline without echoing submitted password values.
    /// </summary>
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.Request.CurrentPassword)
            .NotEmpty();

        RuleFor(command => command.Request.NewPassword)
            .NotEmpty();
    }
}
