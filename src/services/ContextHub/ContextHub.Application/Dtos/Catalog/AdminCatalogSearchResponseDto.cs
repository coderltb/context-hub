namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the CatalogSearch result contract without persistence entities.
/// </summary>
public sealed record AdminCatalogSearchResponseDto
{
    /// <summary>
    /// Gets the materialized results in the selected deterministic order.
    /// </summary>
    public IReadOnlyList<AdminCatalogSearchItemResponseDto> Items { get; init; } = [];

    /// <summary>
    /// Gets the next-page continuation, or null when the result window is exhausted.
    /// </summary>
    public string? NextCursor { get; init; }

}
