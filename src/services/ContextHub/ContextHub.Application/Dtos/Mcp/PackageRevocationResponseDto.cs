namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackageRevocationResponseDto
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
    /// Gets current lifecycle.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    /// <summary>
    /// Gets an optional replacement version.
    /// </summary>
    [JsonPropertyName("replacement_version")]
    public string? ReplacementVersion { get; init; }
}
