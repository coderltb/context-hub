namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Projects approved review content and concurrency metadata.
/// </summary>
public sealed record RecommendationResponseDto
{
    /// <summary>
    /// Gets the queue identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the optional public package identifier.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Gets the recommendation category.
    /// </summary>
    public string RecommendationType { get; init; } = "";

    /// <summary>
    /// Gets the feedback source.
    /// </summary>
    public string Origin { get; init; } = "";

    /// <summary>
    /// Gets the number of merged coded reports.
    /// </summary>
    public long SignalCount { get; init; }

    /// <summary>
    /// Gets the last UTC observation.
    /// </summary>
    public DateTimeOffset LastSeenAt { get; init; }

    /// <summary>
    /// Gets the English review title.
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// Gets the English review description.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Gets server-derived coded context without raw task data.
    /// </summary>
    public JsonElement? Context { get; init; }

    /// <summary>
    /// Gets the current workflow state.
    /// </summary>
    public string Status { get; init; } = "";

    /// <summary>
    /// Gets the UTC creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the creating Owner or null for system feedback.
    /// </summary>
    public Guid? CreatedBy { get; init; }

    /// <summary>
    /// Gets the latest UTC state change.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the changing Owner or null.
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Gets the opaque optimistic update version.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
