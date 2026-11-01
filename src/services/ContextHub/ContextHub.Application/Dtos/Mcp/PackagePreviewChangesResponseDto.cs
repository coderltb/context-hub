namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackagePreviewChangesResponseDto
{
    /// <summary>
    /// Gets the nearest lower stable baseline.
    /// </summary>
    public string? BaselineVersion { get; init; }

    /// <summary>
    /// Gets the factual comparison summary.
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// Gets the public field changes.
    /// </summary>
    public JsonArray Items { get; init; } = [];
}
