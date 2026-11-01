namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the UpdatePackage input contract without persistence entities.
/// </summary>
public sealed record UpdatePackageRequestDto
{
    /// <summary>
    /// Gets the public English name shown in discovery and administration.
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Gets the public English description used by catalog discovery.
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// Gets the catalog visibility independently of release lifecycle.
    /// </summary>
    [JsonRequired]
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Gets the opaque PostgreSQL version returned by the last authoritative read.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
