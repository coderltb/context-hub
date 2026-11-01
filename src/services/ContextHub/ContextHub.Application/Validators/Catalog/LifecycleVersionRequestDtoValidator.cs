namespace ContextHub.Application.Validators.Catalog;

/// <summary>
/// Enforces the published catalog input bounds before I/O.
/// </summary>
public sealed class LifecycleVersionRequestDtoValidator : AbstractValidator<LifecycleVersionRequestDto>
{
    /// <summary>
    /// Registers input constraints without duplicating authoritative database checks.
    /// </summary>
    public LifecycleVersionRequestDtoValidator()
    {
        RuleFor(value => value.ConcurrencyToken).NotEmpty().Must(value => uint.TryParse(value, out _));

        RuleFor(value => value.ReplacementVersion).MaximumLength(64).Matches(CatalogConstants.STABLE_VERSION_PATTERN).When(value => value.ReplacementVersion != null);
    }
}
