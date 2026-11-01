namespace ContextHub.Domain.Entities;

/// <summary>
/// Stores one immutable release with mutable lifecycle and derived search data.
/// </summary>
public sealed class PackageVersion : BaseEntity<Guid>
{
    /// <summary>
    /// Gets or sets the internal owning package identifier.
    /// </summary>
    public Guid PackageId { get; set; }

    /// <summary>
    /// Gets or sets the owning catalog identity.
    /// </summary>
    public Package Package { get; set; } = null!;

    /// <summary>
    /// Gets or sets the exact stable version number.
    /// </summary>
    public string Number { get; set; } = "";

    /// <summary>
    /// Gets or sets the current lifecycle state.
    /// </summary>
    public string Status { get; set; } = "published";

    /// <summary>
    /// Gets or sets the immutable Owner-only JSON manifest.
    /// </summary>
    public string Manifest { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the immutable validated JSON content.
    /// </summary>
    public string Bundle { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the canonical content SHA-256 fingerprint.
    /// </summary>
    public string ContentHash { get; set; } = "";

    /// <summary>
    /// Gets or sets the exact immutable authoring commit.
    /// </summary>
    public string SourceCommitSha { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional replacement stable version.
    /// </summary>
    public string? ReplacementVersion { get; set; }

    /// <summary>
    /// Gets or sets the public derived search text.
    /// </summary>
    public string SearchText { get; set; } = "";

    /// <summary>
    /// Gets or sets the pinned embedding model and preprocessing identity.
    /// </summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>
    /// Gets or sets the optional normalized search embedding.
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Gets or sets the UTC publication instant.
    /// </summary>
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL optimistic concurrency version.
    /// </summary>
    public uint ConcurrencyVersion { get; set; }
}
