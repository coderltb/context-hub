namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Rejects malformed query input before catalog I/O.
/// </summary>
public sealed class GetPackageVersionQueryValidator : AbstractValidator<GetPackageVersionQuery>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public GetPackageVersionQueryValidator()
    {
        RuleFor(value => value.PackageId).NotEmpty().MaximumLength(128).Matches(CatalogConstants.PACKAGE_ID_PATTERN);

        RuleFor(value => value.Version).NotEmpty().MaximumLength(64).Matches(CatalogConstants.STABLE_VERSION_PATTERN);
    }
}
