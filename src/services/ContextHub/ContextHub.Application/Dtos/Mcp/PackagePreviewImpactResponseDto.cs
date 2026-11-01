namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackagePreviewImpactResponseDto
{
    /// <summary>
    /// Gets the changed public areas.
    /// </summary>
    public JsonArray ChangedAreas { get; init; } = [];

    /// <summary>
    /// Gets the factual content difference summary.
    /// </summary>
    public string Summary { get; init; } = "";
}
