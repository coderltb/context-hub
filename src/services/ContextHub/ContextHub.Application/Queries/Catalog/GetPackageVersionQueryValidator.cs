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
        RuleFor(value => value.PackageId).NotEmpty();

        RuleFor(value => value.Version).NotEmpty().Matches(CatalogConstants.STABLE_VERSION_PATTERN);
    }
}
