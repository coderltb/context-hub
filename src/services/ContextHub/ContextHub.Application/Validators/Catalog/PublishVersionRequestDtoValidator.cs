namespace ContextHub.Application.Validators.Catalog;

/// <summary>
/// Enforces the published catalog input bounds before I/O.
/// </summary>
public sealed class PublishVersionRequestDtoValidator : AbstractValidator<PublishVersionRequestDto>
{
    /// <summary>
    /// Registers input constraints without duplicating authoritative database checks.
    /// </summary>
    public PublishVersionRequestDtoValidator()
    {
        RuleFor(value => value.Version).NotEmpty().MaximumLength(64).Matches(CatalogConstants.STABLE_VERSION_PATTERN);

        RuleFor(value => value.AuthoringRepository).NotEmpty().MaximumLength(160);

        RuleFor(value => value.SourceCommitSha).NotEmpty().Matches("^[a-f0-9]{40}$");

        RuleFor(value => value.SourcePath)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(256)
            .Must(value => !value.StartsWith('/') && !value.Contains('\\') && !value.Contains(':') &&
                !value.Split('/').Any(part => part is ".." or "." or ""));
    }
}
