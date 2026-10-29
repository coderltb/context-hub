namespace ContextHub.Authentication.Application.Commands.Users.InviteUser;

/// <summary>
/// Validates account input before native Identity can perform writes.
/// </summary>
public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    /// <summary>
    /// Defines input rules consumed by the existing shared validation behavior.
    /// </summary>
    public InviteUserCommandValidator()
    {
        RuleFor(command => command.Request.UserName)
            .NotEmpty()
            .MinimumLength(AuthenticationConstants.MIN_USER_NAME_LENGTH)
            .MaximumLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH);

        RuleFor(command => command.Request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH);

        RuleFor(command => command.Request.RoleNames)
            .NotEmpty();

        // Reject malformed elements before native role lookup; collection emptiness keeps its use-case meaning.
        RuleForEach(command => command.Request.RoleNames)
            .NotEmpty();
    }
}
