namespace ContextHub.Authentication.Application.Commands.Clients.UpdateClient;

/// <summary>
/// Validates editable client metadata before credential persistence can run.
/// </summary>
public sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    /// <summary>
    /// Uses the shared FluentValidation pipeline rather than HTTP-specific validation.
    /// </summary>
    public UpdateClientCommandValidator()
    {
        RuleFor(command => command.Request.DisplayName)
            .NotEmpty()
            .MaximumLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH);

        RuleFor(command => command.Request.ConcurrencyToken)
            .NotEmpty();
    }
}
