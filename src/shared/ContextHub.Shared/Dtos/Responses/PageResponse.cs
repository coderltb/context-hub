namespace ContextHub.Shared.Dtos.Responses;

/// <summary>
/// Contains list data only; the HTTP adapter owns any outer response envelope.
/// </summary>
/// <typeparam name="T">The item projection returned by the owning feature.</typeparam>
public sealed class PageResponse<T>
{
    /// <summary>
    /// Creates an immutable page snapshot with the documented list fields.
    /// </summary>
    /// <param name="items">The already materialized items.</param>
    /// <param name="request">The validated pagination applied to the query.</param>
    /// <param name="totalCount">The matching count observed separately from the items read.</param>
    public PageResponse(
        IReadOnlyCollection<T> items,
        PageRequest request,
        long totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(request);

        // Validate structural bounds; concurrent writes can change the count between independent reads.
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), SharedErrorConstants.PaginationErrors.TOTAL_COUNT_INVALID);
        }

        if (items.Count > request.PageSize)
        {
            throw new ArgumentException(SharedErrorConstants.PaginationErrors.ITEM_COUNT_INVALID, nameof(items));
        }

        // Copy the caller's collection so later mutations cannot change a response already handed to the adapter.
        Items = Array.AsReadOnly(items.ToArray());
        Page = request.Page;
        PageSize = request.PageSize;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Gets the materialized items; later changes to the input collection cannot change this page.
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Gets the one-based page number.
    /// </summary>
    public int Page { get; }

    /// <summary>
    /// Gets the requested page size.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets the matching count observed by the count read, not a guaranteed snapshot of the items.
    /// </summary>
    public long TotalCount { get; }
}
