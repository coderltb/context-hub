namespace ContextHub.Application.Validators.Catalog;

/// <summary>
/// Enforces the published catalog input bounds before I/O.
/// </summary>
public sealed class PackageListRequestDtoValidator : AbstractValidator<PackageListRequestDto>
{
    /// <summary>
    /// Registers input constraints without duplicating authoritative database checks.
    /// </summary>
    public PackageListRequestDtoValidator()
    {
        RuleFor(value => value.Page).GreaterThanOrEqualTo(1);

        RuleFor(value => value.PageSize).InclusiveBetween(1, 100);

        RuleFor(value => value).Must(value => ((long)value.Page - 1) * value.PageSize <= int.MaxValue).WithMessage("The requested page offset is too large.");

        RuleFor(value => value.Query).MaximumLength(512);

        RuleFor(value => value.PackageType).Must(value => value == null || CatalogConstants.PACKAGE_TYPES.Contains(value));

        RuleFor(value => value.Sort).Must(value => new[] { "packageId", "-packageId", "displayName", "-updatedAt" }.Contains(value));
    }
}
