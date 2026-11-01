namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the CatalogSearch input contract without persistence entities.
/// </summary>
public sealed record CatalogSearchRequestDto
{
    /// <summary>
    /// Gets the search intent; it is never persisted or written to logs.
    /// </summary>
    public string Query { get; init; } = "";

    /// <summary>
    /// Gets the accepted package kinds; an empty list applies no kind filter.
    /// </summary>
    public IReadOnlyList<string>? PackageTypes { get; init; }

    /// <summary>
    /// Gets the canonical tags that must all match the latest eligible release.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Gets the required public package capabilities; all requested values must match.
    /// </summary>
    public IReadOnlyList<string>? Capabilities { get; init; }

    /// <summary>
    /// Gets the maximum number of ranked results, from one through one hundred.
    /// </summary>
    public int Limit { get; init; } = 20;

    /// <summary>
    /// Gets the integrity-protected continuation bound to this query and valid for fifteen minutes.
    /// </summary>
    public string? Cursor { get; init; }
}
