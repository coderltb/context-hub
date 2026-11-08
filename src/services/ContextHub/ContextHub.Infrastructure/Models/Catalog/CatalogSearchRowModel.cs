namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Maps the fixed PostgreSQL search routine result before public DTO projection.
/// </summary>
public sealed class CatalogSearchRowModel
{
    /// <summary>
    /// Gets or sets the package UUID used only by Owner projections.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the public package identifier returned by SQL.
    /// </summary>
    public string PackageId { get; set; } = "";

    /// <summary>
    /// Gets or sets the package kind selected before ranking.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Gets or sets the public display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the public search summary.
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Gets or sets the latest eligible stable release.
    /// </summary>
    public string Version { get; set; } = "";

    /// <summary>
    /// Gets or sets the selected immutable content fingerprint.
    /// </summary>
    public string ContentHash { get; set; } = "";

    /// <summary>
    /// Gets or sets the RRF rank, which is not a probability.
    /// </summary>
    public double Rank { get; set; }

    /// <summary>
    /// Gets or sets the branch/filter reasons actually produced by SQL.
    /// </summary>
    public string[] MatchReasons { get; set; } = [];
}
