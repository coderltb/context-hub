namespace ContextHub.Application.Commands.Mcp;

/// <summary>
/// Rejects malformed configuration before the persistence boundary.
/// </summary>
public sealed class UpdateMcpPromptCommandValidator : AbstractValidator<UpdateMcpPromptCommand>
{
    /// <summary>
    /// Defines configuration rules independently of persisted values.
    /// </summary>
    public UpdateMcpPromptCommandValidator()
    {
        RuleFor(value => value.Id).NotEmpty();
        RuleFor(value => value.Request.ConcurrencyToken).NotEmpty().MaximumLength(32);

        RuleFor(value => value.Request.RemovedArgumentIds).NotNull()
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count);
        RuleForEach(value => value.Request.RemovedArgumentIds).NotEmpty();

        RuleFor(value => value.Request.Content).NotEmpty();
        RuleFor(value => value.Request.Arguments).NotNull()
            .Must(values => values is null || values.All(value => value is not null) &&
                values.Select(value => value!.Name).Distinct(StringComparer.Ordinal).Count() == values.Count);
        RuleForEach(value => value.Request.Arguments).NotNull().ChildRules(argument =>
            argument.RuleFor(value => value!.Name).NotEmpty().MaximumLength(McpPromptConstants.MAX_NAME_LENGTH));
    }
}
