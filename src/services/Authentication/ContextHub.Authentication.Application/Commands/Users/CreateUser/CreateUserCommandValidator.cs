namespace ContextHub.Authentication.Application.Commands.Users.CreateUser;

/// <summary>
/// Validates account input before native Identity can perform writes.
/// </summary>
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    /// <summary>
    /// Defines input rules consumed by the existing shared validation behavior.
    /// </summary>
    public CreateUserCommandValidator()
    {
        RuleFor(command => command.Request.UserName)
            .NotEmpty()
            .MinimumLength(AuthenticationConstants.MIN_USER_NAME_LENGTH)
            .MaximumLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH);

        RuleFor(command => command.Request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH);

        RuleFor(command => command.Request.Password)
            .NotEmpty();
    }
}
