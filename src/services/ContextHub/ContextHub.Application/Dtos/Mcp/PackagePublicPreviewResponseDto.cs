namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackagePublicPreviewResponseDto
{
    /// <summary>
    /// Gets the public package identifier.
    /// </summary>
    public string PackageId { get; init; } = "";

    /// <summary>
    /// Gets the package kind.
    /// </summary>
    public string PackageType { get; init; } = "";

    /// <summary>
    /// Gets the candidate exact version.
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// Gets the public title.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the public summary.
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// Gets public tags.
    /// </summary>
    public JsonNode? Tags { get; init; }

    /// <summary>
    /// Gets public capabilities.
    /// </summary>
    public JsonNode? Capabilities { get; init; }

    /// <summary>
    /// Gets public requirements.
    /// </summary>
    public JsonNode? RequiredCapabilities { get; init; }

    /// <summary>
    /// Gets declared dependencies.
    /// </summary>
    public JsonNode? Dependencies { get; init; }

    /// <summary>
    /// Gets declared conflicts.
    /// </summary>
    public JsonNode? ConflictsWith { get; init; }

    /// <summary>
    /// Gets the dynamic public bundle.
    /// </summary>
    public JsonObject Bundle { get; init; } = new();
}
