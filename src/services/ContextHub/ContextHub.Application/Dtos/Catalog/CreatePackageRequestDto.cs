namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the CreatePackage input contract without persistence entities.
/// </summary>
public sealed record CreatePackageRequestDto
{
    /// <summary>
    /// Gets the stable public package identity, never the internal database UUID.
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
    /// Gets the catalog visibility independently of release lifecycle.
    /// </summary>
    public bool IsActive { get; init; } = true;
}
