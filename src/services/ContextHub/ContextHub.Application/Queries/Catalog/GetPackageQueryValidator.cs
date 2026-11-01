namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Rejects malformed query input before catalog I/O.
/// </summary>
public sealed class GetPackageQueryValidator : AbstractValidator<GetPackageQuery>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public GetPackageQueryValidator()
    {
        RuleFor(value => value.PackageId).NotEmpty().MaximumLength(128).Matches(CatalogConstants.PACKAGE_ID_PATTERN);
    }
}
