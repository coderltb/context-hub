namespace ContextHub.Authentication.Application.Commands.Clients.IssueClientToken;

/// <summary>
/// Validates exact tool identifiers before the credential service can create or persist material.
/// </summary>
public sealed class IssueClientTokenCommandValidator : AbstractValidator<IssueClientTokenCommand>
{
    /// <summary>
    /// Allows an explicit empty grant set but rejects null, wildcard, path and malformed tool identifiers.
    /// </summary>
    public IssueClientTokenCommandValidator()
    {
        // Validate the explicit grant list's shape without inventing wildcard or feature-level permissions.
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
