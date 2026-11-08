namespace ContextHub.Application.Commands.Mcp;

/// <summary>
/// Rejects unsupported public input before Infrastructure performs I/O.
/// </summary>
public sealed class UpdateMcpToolCommandValidator : AbstractValidator<UpdateMcpToolCommand>
{
    /// <summary>
    /// Registers the fixed contract rules for this operation.
    /// </summary>
    public UpdateMcpToolCommandValidator()
    {
        RuleFor(value => value.ToolId).NotEmpty();
        RuleFor(value => value.Request.Title).Must(value => value is null || !string.IsNullOrWhiteSpace(value));
        RuleFor(value => value.Request.Description).Must(value => value is null || !string.IsNullOrWhiteSpace(value));
        RuleFor(value => value.Request.ConcurrencyToken).NotEmpty().MaximumLength(32);
    }
}
