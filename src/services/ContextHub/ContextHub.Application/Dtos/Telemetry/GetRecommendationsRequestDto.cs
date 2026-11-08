namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Groups supported review queue filters and paging.
/// </summary>
public sealed record GetRecommendationsRequestDto
{
    /// <summary>
    /// Gets the one-based page.
    /// </summary>
    public int Page { get; init; } = SharedConstants.PaginationVariables.FIRST_PAGE;

    /// <summary>
    /// Gets the bounded page size.
    /// </summary>
    public int PageSize { get; init; } = SharedConstants.PaginationVariables.DEFAULT_PAGE_SIZE;

    /// <summary>
    /// Gets an optional public package filter.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Gets an optional fixed category.
    /// </summary>
    public string? RecommendationType { get; init; }

    /// <summary>
    /// Gets an optional workflow state.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Gets timestamp or signal-count ordering with stable identity tie-breaking.
    /// </summary>
    public string Sort { get; init; } = "-createdAt";
}
