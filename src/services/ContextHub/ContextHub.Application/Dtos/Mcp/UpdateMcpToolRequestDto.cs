namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Changes only enablement using optimistic concurrency.
/// </summary>
public sealed record UpdateMcpToolRequestDto
{
    /// <summary>
    /// Gets an optional title replacement; null keeps the current value.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets an optional summary replacement; null keeps the current value.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the desired global state.
    /// </summary>
    [JsonRequired]
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the opaque expected database version.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
