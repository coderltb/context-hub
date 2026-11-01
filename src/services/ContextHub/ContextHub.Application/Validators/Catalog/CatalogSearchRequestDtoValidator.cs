namespace ContextHub.Application.Validators.Catalog;

/// <summary>
/// Enforces the same bounded request-only filters accepted by the MCP search schema before REST I/O.
/// </summary>
public sealed class CatalogSearchRequestDtoValidator : AbstractValidator<CatalogSearchRequestDto>
{
    /// <summary>
    /// Registers contract bounds without repeating lifecycle or database checks.
    /// </summary>
    public CatalogSearchRequestDtoValidator()
    {
        RuleFor(value => value.Query).NotEmpty().MaximumLength(512);

        RuleFor(value => value.Limit).InclusiveBetween(1, 100);
        RuleFor(value => value.Cursor).MaximumLength(4096);

        RuleFor(value => value.PackageTypes).Must(values => values is null ||
            values.Count <= 4 && values.Distinct(StringComparer.Ordinal).Count() == values.Count);
        RuleForEach(value => value.PackageTypes).Must(value => CatalogConstants.PACKAGE_TYPES.Contains(value));

        RuleFor(value => value.Tags).Must(values => values is null ||
            values.Count <= 20 && values.Distinct(StringComparer.Ordinal).Count() == values.Count);
        RuleForEach(value => value.Tags).NotEmpty().MaximumLength(64).Matches("^[a-z0-9][a-z0-9._-]{0,63}$");

        RuleFor(value => value.Capabilities).Must(values => values is null ||
            values.Count <= 20 && values.Distinct(StringComparer.Ordinal).Count() == values.Count);
        RuleForEach(value => value.Capabilities).Must(value => CatalogConstants.PACKAGE_CAPABILITIES.Contains(value));
    }
}
