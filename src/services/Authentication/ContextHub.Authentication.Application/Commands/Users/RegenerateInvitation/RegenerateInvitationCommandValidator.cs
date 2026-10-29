namespace ContextHub.Authentication.Application.Commands.Users.RegenerateInvitation;

/// <summary>
/// Validates account input before native Identity can perform writes.
/// </summary>
public sealed class RegenerateInvitationCommandValidator : AbstractValidator<RegenerateInvitationCommand>
{
    /// <summary>
    /// Defines input rules consumed by the existing shared validation behavior.
    /// </summary>
    public RegenerateInvitationCommandValidator()
    {
        RuleFor(command => command.Request.ConcurrencyToken)
            .NotEmpty();
    }
}
