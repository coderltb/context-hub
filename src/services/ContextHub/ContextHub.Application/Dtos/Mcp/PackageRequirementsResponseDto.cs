namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackageRequirementsResponseDto
{
    /// <summary>
    /// Gets the public package identifier.
    /// </summary>
    [JsonPropertyName("package_id")]
    public string PackageId { get; init; } = "";

    /// <summary>
    /// Gets the exact version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    /// <summary>
    /// Gets the public capability requirements.
    /// </summary>
    [JsonPropertyName("required_capabilities")]
    public JsonNode? RequiredCapabilities { get; init; }
}
