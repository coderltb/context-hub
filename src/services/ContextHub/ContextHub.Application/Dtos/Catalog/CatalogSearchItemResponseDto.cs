namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the CatalogSearchItem result contract without persistence entities.
/// </summary>
public sealed record CatalogSearchItemResponseDto
{
    /// <summary>
    /// Gets the stable public package identity, never the internal database UUID.
    /// </summary>
    public string PackageId { get; init; } = "";

    /// <summary>
    /// Gets the closed package-kind vocabulary value.
    /// </summary>
    public string PackageType { get; init; } = "";

    /// <summary>
    /// Gets the public English name shown in discovery and administration.
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Gets the public English description used by catalog discovery.
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// Gets the exact stable X.Y.Z release; lookup requests may omit it to select the latest published release.
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// Gets the filter and ranking-branch codes actually produced by the database.
    /// </summary>
    public IReadOnlyList<string> MatchReasons { get; init; } = [];
}
