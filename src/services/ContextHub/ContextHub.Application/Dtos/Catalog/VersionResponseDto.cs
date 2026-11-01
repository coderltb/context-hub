namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the Version result contract without persistence entities.
/// </summary>
public sealed record VersionResponseDto
{
    /// <summary>
    /// Gets the stable public package identity, never the internal database UUID.
    /// </summary>
    public string PackageId { get; init; } = "";

    /// <summary>
    /// Gets the exact stable X.Y.Z release; lookup requests may omit it to select the latest published release.
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// Gets the current lifecycle state from the database authority.
    /// </summary>
    public string Status { get; init; } = "";

    /// <summary>
    /// Gets the SHA-256 fingerprint of the canonical immutable manifest and bundle.
    /// </summary>
    public string ContentHash { get; init; } = "";

    /// <summary>
    /// Gets the optional existing non-revoked release suggested as a replacement.
    /// </summary>
    public string? ReplacementVersion { get; init; }

    /// <summary>
    /// Gets the server-owned UTC publication instant.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; }

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

    /// <summary>
    /// Gets the Owner-only immutable manifest, including reviewed provenance.
    /// </summary>
    public JsonObject Manifest { get; init; } = new();

    /// <summary>
    /// Gets the validated immutable guidance; the server does not execute its instructions.
    /// </summary>
    public JsonObject Bundle { get; init; } = new();

    /// <summary>
    /// Gets the exact full Git commit reviewed for this publication.
    /// </summary>
    public string SourceCommitSha { get; init; } = "";
}
