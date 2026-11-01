namespace ContextHub.Application.Commands.Mcp;

/// <summary>
/// Rejects malformed configuration before the persistence boundary.
/// </summary>
public sealed class CreateMcpPromptCommandValidator : AbstractValidator<CreateMcpPromptCommand>
{
    /// <summary>
    /// Defines configuration rules independently of persisted values.
    /// </summary>
    public CreateMcpPromptCommandValidator()
    {
        RuleFor(value => value.Request.Name).NotEmpty().MaximumLength(McpPromptConstants.MAX_NAME_LENGTH).Matches("^[a-z][a-z0-9_]*$");

        RuleFor(value => value.Request.Content).NotEmpty();
        RuleFor(value => value.Request.Arguments).NotNull()
            .Must(values => values is null || values.All(value => value is not null) &&
                values.Select(value => value!.Name).Distinct(StringComparer.Ordinal).Count() == values.Count);
        RuleForEach(value => value.Request.Arguments).NotNull().ChildRules(argument =>
            argument.RuleFor(value => value!.Name).NotEmpty().MaximumLength(McpPromptConstants.MAX_NAME_LENGTH));
    }
}
