namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackageMetadataResponseDto
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
    /// Gets the public title.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Gets the public summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    /// <summary>
    /// Gets the publication timestamp.
    /// </summary>
    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>
    /// Gets applicable usage guidance.
    /// </summary>
    [JsonPropertyName("applicability")]
    public PackageApplicabilityResponseDto Applicability { get; init; } = new();

    /// <summary>
    /// Gets the public manifest allowlist.
    /// </summary>
    [JsonPropertyName("manifest")]
    public JsonObject Manifest { get; init; } = new();
}
