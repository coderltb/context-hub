namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackageCompatibilityResponseDto
{
    /// <summary>
    /// Gets the public package identifier.
    /// </summary>
    [JsonPropertyName("package_id")]
    public string PackageId { get; init; } = "";

    /// <summary>
    /// Gets the selected version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    /// <summary>
    /// Gets whether every requirement was declared.
    /// </summary>
    [JsonPropertyName("compatible")]
    public bool Compatible { get; init; }

    /// <summary>
    /// Gets actual missing requirements.
    /// </summary>
    [JsonPropertyName("issues")]
    public IReadOnlyList<PackageCompatibilityIssueResponseDto> Issues { get; init; } = [];
}
