namespace ContextHub.Application.Validators.Catalog;

/// <summary>
/// Enforces the published catalog input bounds before I/O.
/// </summary>
public sealed class CreatePackageRequestDtoValidator : AbstractValidator<CreatePackageRequestDto>
{
    /// <summary>
    /// Registers input constraints without duplicating authoritative database checks.
    /// </summary>
    public CreatePackageRequestDtoValidator()
    {
        RuleFor(value => value.PackageId).NotEmpty().MaximumLength(128).Matches(CatalogConstants.PACKAGE_ID_PATTERN);

        RuleFor(value => value.PackageType).Must(value => CatalogConstants.PACKAGE_TYPES.Contains(value));

        RuleFor(value => value.DisplayName).NotEmpty().MaximumLength(200);

        RuleFor(value => value.Summary).NotEmpty().MaximumLength(1000);
    }
}
