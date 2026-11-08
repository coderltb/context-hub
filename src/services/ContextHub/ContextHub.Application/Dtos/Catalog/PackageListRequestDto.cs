namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the PackageList input contract without persistence entities.
/// </summary>
public sealed record PackageListRequestDto
{
    /// <summary>
    /// Gets the one-based page number; paging never changes the selected filters.
    /// </summary>
    public int Page { get; init; } = SharedConstants.PaginationVariables.FIRST_PAGE;

    /// <summary>
    /// Gets the number of items requested, from one through one hundred.
    /// </summary>
    public int PageSize { get; init; } = SharedConstants.PaginationVariables.DEFAULT_PAGE_SIZE;

    /// <summary>
    /// Gets the search intent; it is never persisted or written to logs.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Gets the closed package-kind vocabulary value.
    /// </summary>
    public string? PackageType { get; init; }

    /// <summary>
    /// Gets the catalog visibility independently of release lifecycle.
    /// </summary>
    public bool? IsActive { get; init; }

    /// <summary>
    /// Gets the allowlisted administrative ordering, independent of ranked search.
    /// </summary>
    public string Sort { get; init; } = CatalogConstants.PACKAGE_KEY_SORT;
}
