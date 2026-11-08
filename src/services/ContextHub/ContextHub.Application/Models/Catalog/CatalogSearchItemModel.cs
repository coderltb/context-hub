namespace ContextHub.Application.Models.Catalog;

/// <summary>
/// Carries one ranked search result independently of its transport projection.
/// </summary>
public sealed record CatalogSearchItemModel
{
    /// <summary>
    /// Gets the internal package UUID; public adapters use Key instead.
    /// </summary>
    public Guid PackageId { get; init; }

    /// <summary>
    /// Gets the immutable public key shown alongside the Owner identity.
    /// </summary>
    public string Key { get; init; } = "";

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
