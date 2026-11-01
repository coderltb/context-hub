namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackageBundleResponseDto
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

    /// <summary>
    /// Gets the package kind.
    /// </summary>
    [JsonPropertyName("package_type")]
    public string PackageType { get; init; } = "";

    /// <summary>
    /// Gets the immutable content hash.
    /// </summary>
    [JsonPropertyName("content_hash")]
    public string ContentHash { get; init; } = "";

    /// <summary>
    /// Gets the dynamic guidance bundle.
    /// </summary>
    [JsonPropertyName("bundle")]
    public JsonObject Bundle { get; init; } = new();
}
