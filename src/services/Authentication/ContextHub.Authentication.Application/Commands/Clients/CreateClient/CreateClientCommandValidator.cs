namespace ContextHub.Authentication.Application.Commands.Clients.CreateClient;

/// <summary>
/// Validates editable client metadata before credential persistence can run.
/// </summary>
public sealed class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    /// <summary>
    /// Uses the shared FluentValidation pipeline rather than HTTP-specific validation.
    /// </summary>
    public CreateClientCommandValidator()
    {
        // Validate editable metadata separately from credential grants; neither group supplies audit values.
        RuleFor(command => command.Request.DisplayName)
            .NotEmpty()
            .MaximumLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH);

        // Empty grants intentionally deny all tools; a missing collection is not an implicit all-tools grant.
        RuleFor(command => command.Request.AllowedTools)
            .NotNull();

        RuleForEach(command => command.Request.AllowedTools)
            .NotEmpty()
            .MaximumLength(AuthenticationConstants.MAX_TOOL_NAME_LENGTH)
            .Matches(AuthenticationConstants.TOOL_NAME_PATTERN);

        // Null is unlimited; an explicit expiry must still be usable when issuance begins.
        RuleFor(command => command.Request.ExpiresAt)
            .Must(expiry => expiry is null || expiry > DateTimeOffset.UtcNow)
            .WithMessage(AuthenticationErrorConstants.CLIENT_TOKEN_EXPIRY_INVALID);
    }
}
