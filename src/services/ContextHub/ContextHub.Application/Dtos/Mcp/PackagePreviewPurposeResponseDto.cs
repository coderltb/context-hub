namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record PackagePreviewPurposeResponseDto
{
    /// <summary>
    /// Gets the package summary.
    /// </summary>
    public string Summary { get; init; } = "";

    /// <summary>
    /// Gets the intended usage.
    /// </summary>
    public string WhenToUse { get; init; } = "";

    /// <summary>
    /// Gets excluded usage.
    /// </summary>
    public string WhenNotToUse { get; init; } = "";
}
