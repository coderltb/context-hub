namespace ContextHub.Application.Validators.Integration;

/// <summary>
/// Validates the public UpdateRepositoryCommand input before native IO.
/// </summary>
public sealed class UpdateRepositoryCommandValidator : AbstractValidator<UpdateRepositoryCommand>
{
    /// <summary>
    /// Composes only the externally supplied input constraints.
    /// </summary>
    public UpdateRepositoryCommandValidator()
    {
        RuleFor(value => value.RepositoryId).NotEmpty();
        RuleFor(value => value.Request.RepositoryUrl).NotEmpty().MaximumLength(2048);
        RuleFor(value => value.Request.DefaultRef)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(256)
            .Must(value => !value.Any(char.IsControl));
        RuleFor(value => value.Request.ConcurrencyToken)
            .NotEmpty()
            .Must(value => uint.TryParse(value, out _));
    }
}
