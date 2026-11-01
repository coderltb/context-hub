namespace ContextHub.Application.Validators.Integration;

/// <summary>
/// Validates the public SyncRepositoryCommand input before native IO.
/// </summary>
public sealed class SyncRepositoryCommandValidator : AbstractValidator<SyncRepositoryCommand>
{
    /// <summary>
    /// Composes only the externally supplied input constraints.
    /// </summary>
    public SyncRepositoryCommandValidator()
    {
        RuleFor(value => value.RepositoryId).NotEmpty();
        RuleFor(value => value.Request.ConcurrencyToken)
            .NotEmpty()
            .Must(value => uint.TryParse(value, out _));
    }
}
