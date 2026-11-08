namespace ContextHub.Application.Models.Catalog;

/// <summary>
/// Carries the common ranked page before Admin or MCP output mapping.
/// </summary>
public sealed record CatalogSearchResultModel
{
    /// <summary>
    /// Gets the materialized results in the selected deterministic order.
    /// </summary>
    public IReadOnlyList<CatalogSearchItemModel> Items { get; init; } = [];

    /// <summary>
    /// Gets the next-page continuation, or null when the result window is exhausted.
    /// </summary>
    public string? NextCursor { get; init; }

}
