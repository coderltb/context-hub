namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the LifecycleVersion input contract without persistence entities.
/// </summary>
public sealed record LifecycleVersionRequestDto
{
    /// <summary>
    /// Gets the optional existing non-revoked release suggested as a replacement.
    /// </summary>
    public string? ReplacementVersion { get; init; }

    /// <summary>
    /// Gets the opaque PostgreSQL version returned by the last authoritative read.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
