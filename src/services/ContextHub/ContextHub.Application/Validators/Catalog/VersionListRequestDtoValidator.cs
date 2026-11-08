namespace ContextHub.Application.Validators.Catalog;

/// <summary>
/// Enforces the published catalog input bounds before I/O.
/// </summary>
public sealed class VersionListRequestDtoValidator : AbstractValidator<VersionListRequestDto>
{
    /// <summary>
    /// Registers input constraints without duplicating authoritative database checks.
    /// </summary>
    public VersionListRequestDtoValidator()
    {
        RuleFor(value => value.Page).GreaterThanOrEqualTo(1);

        RuleFor(value => value.PageSize).InclusiveBetween(1, 100);

        RuleFor(value => value).Must(value => ((long)value.Page - 1) * value.PageSize <= int.MaxValue).WithMessage("The requested page offset is too large.");

        RuleFor(value => value.Status).Must(value => value == null || CatalogConstants.VERSION_STATUSES.Contains(value));

        RuleFor(value => value.Sort).Must(value => CatalogConstants.VERSION_SORTS.Contains(value));
    }
}
