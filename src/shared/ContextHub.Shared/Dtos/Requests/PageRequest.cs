namespace ContextHub.Shared.Dtos.Requests;

/// <summary>
/// Describes validated page-number pagination; sorting remains owned by each feature.
/// </summary>
public sealed record PageRequest
{
    /// <summary>
    /// Creates pagination without silently clamping invalid input.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of items, from one through one hundred.</param>
    public PageRequest(
        int page = SharedConstants.PaginationVariables.FIRST_PAGE,
        int pageSize = SharedConstants.PaginationVariables.DEFAULT_PAGE_SIZE)
    {
        if (page < SharedConstants.PaginationVariables.FIRST_PAGE)
        {
            throw new ArgumentOutOfRangeException(nameof(page), SharedErrorConstants.PaginationErrors.PAGE_INVALID);
        }

        if (pageSize is < 1 or > SharedConstants.PaginationVariables.MAX_PAGE_SIZE)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), SharedErrorConstants.PaginationErrors.PAGE_SIZE_INVALID);
        }

        Page = page;
        PageSize = pageSize;
    }

    /// <summary>
    /// Gets the one-based page number.
    /// </summary>
    public int Page { get; }

    /// <summary>
    /// Gets the bounded page size.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets the offset using wide arithmetic; repositories must respect their provider's limits.
    /// </summary>
    [JsonIgnore]
    // Widen before subtraction and multiplication; large valid page numbers exceed Int32 offsets.
    public long Offset => ((long)Page - 1) * PageSize;
}
