namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackageResolveResponseDto
{
    /// <summary>
    /// Gets the exact root selection.
    /// </summary>
    [JsonPropertyName("root")]
    public PackageIdentityResponseDto Root { get; init; } = new();

    /// <summary>
    /// Gets the dependency-before-root closure.
    /// </summary>
    [JsonPropertyName("packages")]
    public IReadOnlyList<PackageBundleResponseDto> Packages { get; init; } = [];
}
