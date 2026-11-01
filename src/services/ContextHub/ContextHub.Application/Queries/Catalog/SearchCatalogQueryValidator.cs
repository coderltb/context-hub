namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Rejects malformed query input before catalog I/O.
/// </summary>
public sealed class SearchCatalogQueryValidator : AbstractValidator<SearchCatalogQuery>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public SearchCatalogQueryValidator()
    {
        RuleFor(value => value.Request).NotNull().SetValidator(new CatalogSearchRequestDtoValidator());
    }
}
