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
        RuleFor(value => value.Name).NotEmpty().MaximumLength(120);
    }
}
