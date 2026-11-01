namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Exposes approved repository metadata without source content or credentials.
/// </summary>
public sealed class RepositoryResponseDto
{
    /// <summary>
    /// Gets the integration identifier.
    /// </summary>
    public Guid Id { get; init; } = Guid.Empty;

    /// <summary>
    /// Gets the canonical repository URL.
    /// </summary>
    public string RepositoryUrl { get; init; } = "";

    /// <summary>
    /// Gets the persisted default reference.
    /// </summary>
    public string DefaultRef { get; init; } = "";

    /// <summary>
    /// Gets the current enabled state.
    /// </summary>
    public bool IsEnabled { get; init; } = false;

    /// <summary>
    /// Gets the last exact successfully inspected commit.
    /// </summary>
    public string? LastSyncedCommitSha { get; init; } = null;

    /// <summary>
    /// Gets success or failed, or null before the first sync.
    /// </summary>
    public string? LastSyncStatus { get; init; } = null;

    /// <summary>
    /// Gets the UTC time of the latest explicit sync.
    /// </summary>
    public DateTimeOffset? LastSyncedAt { get; init; } = null;

    /// <summary>
    /// Gets a safe bounded diagnostic without upstream details.
    /// </summary>
    public string? LastErrorMessage { get; init; } = null;

    /// <summary>
    /// Gets the original UTC creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets the creating Owner identifier.
    /// </summary>
    public Guid? CreatedBy { get; init; } = null;

    /// <summary>
    /// Gets the latest UTC update time.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets the latest Owner identifier.
    /// </summary>
    public Guid? UpdatedBy { get; init; } = null;

    /// <summary>
    /// Gets the opaque PostgreSQL row version.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";

}
