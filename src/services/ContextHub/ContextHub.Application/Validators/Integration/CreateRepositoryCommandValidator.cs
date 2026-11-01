namespace ContextHub.Application.Validators.Integration;

/// <summary>
/// Validates the public CreateRepositoryCommand input before native IO.
/// </summary>
public sealed class CreateRepositoryCommandValidator : AbstractValidator<CreateRepositoryCommand>
{
    /// <summary>
    /// Composes only the externally supplied input constraints.
    /// </summary>
    public CreateRepositoryCommandValidator()
    {
        RuleFor(value => value.Request.RepositoryUrl).NotEmpty().MaximumLength(2048);
        RuleFor(value => value.Request.DefaultRef)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(256)
            .Must(value => !value.Any(char.IsControl));
    }
}
