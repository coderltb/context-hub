namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Selects one release for a live revocation check; omitted version selects the latest stable published release.
/// </summary>
public sealed record RevocationRequestDto
{
    /// <summary>
    /// Gets the canonical public package identifier.
    /// </summary>
    public string PackageId { get; init; } = "";

    /// <summary>
    /// Gets the optional exact stable version whose lifecycle is being checked.
    /// </summary>
    public string? Version { get; init; }
}
