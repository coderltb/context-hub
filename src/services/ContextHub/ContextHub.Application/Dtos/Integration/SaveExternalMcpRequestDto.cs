namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Saves canonical connection metadata returned by preview.
/// </summary>
public sealed class SaveExternalMcpRequestDto
{
    /// <summary>
    /// Gets the closed canonical connection configuration.
    /// </summary>
    public JsonElement Config { get; init; }

    /// <summary>
    /// Gets the expected version when updating; omitted for create.
    /// </summary>
    public string? ConcurrencyToken { get; init; } = null;

}
