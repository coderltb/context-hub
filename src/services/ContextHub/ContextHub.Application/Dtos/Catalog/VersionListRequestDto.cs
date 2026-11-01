namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the VersionList input contract without persistence entities.
/// </summary>
public sealed record VersionListRequestDto
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
    /// Gets the current lifecycle state from the database authority.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Gets the allowlisted administrative ordering, independent of ranked search.
    /// </summary>
    public string Sort { get; init; } = "-publishedAt";
}
