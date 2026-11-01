namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Rejects malformed query input before catalog I/O.
/// </summary>
public sealed class GetPackagesQueryValidator : AbstractValidator<GetPackagesQuery>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public GetPackagesQueryValidator()
    {
        RuleFor(value => value.Request).NotNull().SetValidator(new PackageListRequestDtoValidator());
    }
}
