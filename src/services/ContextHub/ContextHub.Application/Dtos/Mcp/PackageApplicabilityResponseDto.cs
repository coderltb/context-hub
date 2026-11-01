namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackageApplicabilityResponseDto
{
    /// <summary>
    /// Gets applicable usage guidance.
    /// </summary>
    [JsonPropertyName("when_to_use")]
    public string WhenToUse { get; init; } = "";

    /// <summary>
    /// Gets excluded usage guidance.
    /// </summary>
    [JsonPropertyName("when_not_to_use")]
    public string WhenNotToUse { get; init; } = "";
}
