namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the LifecycleVersion input contract without persistence entities.
/// </summary>
public sealed record LifecycleVersionRequestDto
{
    /// <summary>
    /// Gets an optional different published SemVer from this package.
    /// </summary>
    public string? ReplacementVersion { get; init; }

    /// <summary>
    /// Gets the opaque PostgreSQL version returned by the last authoritative read.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
