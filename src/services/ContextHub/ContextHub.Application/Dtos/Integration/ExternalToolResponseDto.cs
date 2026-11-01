namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Returns an upstream definition only within the Owner preview boundary.
/// </summary>
public sealed class ExternalToolResponseDto
{
    /// <summary>
    /// Gets the upstream tool name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the upstream display title.
    /// </summary>
    public string? Title { get; init; } = null;

    /// <summary>
    /// Gets the upstream description.
    /// </summary>
    public string? Description { get; init; } = null;

    /// <summary>
    /// Gets the advertised input schema.
    /// </summary>
    public JsonElement InputSchema { get; init; } = default;

    /// <summary>
    /// Gets whether an independent reviewed policy permits reads.
    /// </summary>
    public bool IsEligible { get; init; }

}

