namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Lists immutable release metadata without returning Owner-only source content on every row.
/// </summary>
public sealed record VersionSummaryResponseDto
{
    /// <summary>
    /// Gets the owning package UUID.
    /// </summary>
    public Guid PackageId { get; init; }

    /// <summary>
    /// Gets the immutable public package key.
    /// </summary>
    public string Key { get; init; } = "";

    /// <summary>
    /// Gets the exact stable release number.
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// Gets the current release lifecycle.
    /// </summary>
    public string Status { get; init; } = "";

    /// <summary>
    /// Gets the immutable-content SHA-256 fingerprint.
    /// </summary>
    public string ContentHash { get; init; } = "";

    /// <summary>
    /// Gets the replacement SemVer from the same package.
    /// </summary>
    public string? ReplacementVersion { get; init; }

    /// <summary>
    /// Gets the server-owned UTC publication instant.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// Gets the last lifecycle or derived-projection update.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
