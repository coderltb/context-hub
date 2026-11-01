namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackageCompatibilityIssueResponseDto
{
    /// <summary>
    /// Gets the compatibility issue category.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = "missing_capability";

    /// <summary>
    /// Gets the missing declared capability.
    /// </summary>
    [JsonPropertyName("capability")]
    public string Capability { get; init; } = "";

    /// <summary>
    /// Gets the fixed issue description.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = "The client does not declare a required capability.";
}
