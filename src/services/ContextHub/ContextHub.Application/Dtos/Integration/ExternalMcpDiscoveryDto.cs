namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Describes a rebuildable metadata snapshot without exposing cache keys.
/// </summary>
public sealed class ExternalMcpDiscoveryDto
{
    /// <summary>
    /// Gets unknown, ready or unavailable.
    /// </summary>
    public string Status { get; init; } = "unknown";

    /// <summary>
    /// Gets the number of currently reviewed eligible tools.
    /// </summary>
    public int ToolCount { get; init; }

    /// <summary>
    /// Gets the latest successful snapshot time.
    /// </summary>
    public DateTimeOffset? RefreshedAt { get; init; }

}

