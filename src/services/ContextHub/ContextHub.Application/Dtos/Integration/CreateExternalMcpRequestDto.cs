namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Creates one canonical connection without accepting a caller-owned database identity.
/// </summary>
public sealed class CreateExternalMcpRequestDto
{
    /// <summary>
    /// Gets the unique editable connection label.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the canonical configuration accepted by the existing validator.
    /// </summary>
    public JsonElement Config { get; init; }
}
