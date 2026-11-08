namespace ContextHub.Application.Validators.Integration;

/// <summary>
/// Validates the public DeleteExternalMcpCommand input before native IO.
/// </summary>
public sealed class DeleteExternalMcpCommandValidator : AbstractValidator<DeleteExternalMcpCommand>
{
    /// <summary>
    /// Composes only the externally supplied input constraints.
    /// </summary>
    public DeleteExternalMcpCommandValidator()
    {
        RuleFor(value => value.ConnectionId).NotEmpty();
        RuleFor(value => value.ConcurrencyToken)
            .NotEmpty()
            .Must(value => value is not null && uint.TryParse(value.Trim('"'), out _));
    }
}
