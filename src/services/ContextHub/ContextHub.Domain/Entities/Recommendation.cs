namespace ContextHub.Domain.Entities;

/// <summary>
/// Tracks an Owner-reviewed improvement or a bounded coded signal.
/// </summary>
public sealed class Recommendation : BaseEntity<Guid>
{
    /// <summary>
    /// Gets or sets the optional related catalog package.
    /// </summary>
    public Guid? PackageId { get; set; }

    /// <summary>
    /// Gets or sets the recommendation category.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Gets or sets the trusted source of this recommendation.
    /// </summary>
    public string Origin { get; set; } = "owner";

    /// <summary>
    /// Gets or sets the fingerprint used only for client signals.
    /// </summary>
    public string? DedupeKey { get; set; }

    /// <summary>
    /// Gets or sets the safe English title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the safe English description.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional server-created coded JSON context.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Gets or sets the Owner-controlled workflow state.
    /// </summary>
    public string Status { get; set; } = "new";

    /// <summary>
    /// Gets or sets the number of merged signals.
    /// </summary>
    public long SignalCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the last UTC observation instant.
    /// </summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL optimistic concurrency version.
    /// </summary>
    public uint ConcurrencyVersion { get; set; }
}
