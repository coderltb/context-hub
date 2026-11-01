namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Groups administrative repository paging, search, filters and ordering.
/// </summary>
public sealed class GetRepositoriesRequestDto
{
    /// <summary>
    /// Gets the one-based page number.
    /// </summary>
    public int Page { get; init; } = SharedConstants.PaginationVariables.FIRST_PAGE;

    /// <summary>
    /// Gets the bounded page size.
    /// </summary>
    public int PageSize { get; init; } = SharedConstants.PaginationVariables.DEFAULT_PAGE_SIZE;

    /// <summary>
    /// Gets literal URL search text; null skips search.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Gets the optional enabled-state filter.
    /// </summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Gets success, failed or never; null skips this filter.
    /// </summary>
    public string? LastSyncStatus { get; init; }

    /// <summary>
    /// Gets comma-separated repositoryUrl or lastSyncedAt fields with asc or desc.
    /// </summary>
    public string? Sort { get; init; } = IntegrationConstants.DEFAULT_REPOSITORY_SORT;

}

