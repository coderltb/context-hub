namespace ContextHub.Domain.Entities;

/// <summary>
/// Records an allowlisted authoring repository without copying source history.
/// </summary>
public sealed class Repository : BaseEntity<Guid>
{
    /// <summary>
    /// Gets or sets the canonical GitHub repository URL.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// Gets or sets the configured branch, tag or commit.
    /// </summary>
    public string DefaultRef { get; set; } = "";

    /// <summary>
    /// Gets or sets whether this source may be read.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the exact last successfully inspected commit.
    /// </summary>
    public string? LastSyncedCommitSha { get; set; }

    /// <summary>
    /// Gets or sets the safe result of the latest sync.
    /// </summary>
    public string? LastSyncStatus { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the latest sync.
    /// </summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// Gets or sets the safe diagnostic without upstream payload or credentials.
    /// </summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL optimistic concurrency version.
    /// </summary>
    public uint ConcurrencyVersion { get; set; }
}
