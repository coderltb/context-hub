namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the Package result contract without persistence entities.
/// </summary>
public sealed record PackageResponseDto
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
    /// Gets the catalog visibility independently of release lifecycle.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the highest stable published version, or null before publication.
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// Gets the UTC creation instant assigned by persistence.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the UTC time of the latest persisted change.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the logical Owner identifier assigned by the audit boundary.
    /// </summary>
    public Guid? CreatedBy { get; init; }

    /// <summary>
    /// Gets the logical actor of the latest update; system work may leave it null.
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Gets the opaque PostgreSQL version returned by the last authoritative read.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
