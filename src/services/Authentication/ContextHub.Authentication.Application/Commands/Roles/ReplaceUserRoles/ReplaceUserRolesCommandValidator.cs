namespace ContextHub.Authentication.Application.Commands.Roles.ReplaceUserRoles;

/// <summary>
/// Validates account input before native Identity can perform writes.
/// </summary>
public sealed class ReplaceUserRolesCommandValidator : AbstractValidator<ReplaceUserRolesCommand>
{
    /// <summary>
    /// Defines input rules consumed by the existing shared validation behavior.
    /// </summary>
    public ReplaceUserRolesCommandValidator()
    {
        RuleFor(command => command.Request.ConcurrencyToken)
            .NotEmpty();

        RuleFor(command => command.Request.RoleIds)
            .NotNull().Must(ids => ids is null || ids.Distinct().Count() == ids.Length);

        // Reject malformed elements before native role lookup; collection emptiness keeps its use-case meaning.
        RuleForEach(command => command.Request.RoleIds)
            .NotEmpty();
    }
}
