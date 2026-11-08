namespace ContextHub.Application.Validators.Integration;

/// <summary>
/// Validates the public SaveExternalMcpCommand input before native IO.
/// </summary>
public sealed class SaveExternalMcpCommandValidator : AbstractValidator<SaveExternalMcpCommand>
{
    /// <summary>
    /// Composes only the externally supplied input constraints.
    /// </summary>
    public SaveExternalMcpCommandValidator()
    {
        RuleFor(value => value.ConnectionId).NotEmpty();
        RuleFor(value => value.Request.Name).NotEmpty().MaximumLength(120);
        RuleFor(value => value.Request.ConcurrencyToken).NotEmpty().Must(value => uint.TryParse(value, out _));
    }
}
