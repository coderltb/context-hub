namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Rejects malformed query input before catalog I/O.
/// </summary>
public sealed class GetPackageVersionsQueryValidator : AbstractValidator<GetPackageVersionsQuery>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public GetPackageVersionsQueryValidator()
    {
        RuleFor(value => value.PackageId).NotEmpty();

        RuleFor(value => value.Request).NotNull().SetValidator(new VersionListRequestDtoValidator());
    }
}
