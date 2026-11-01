namespace ContextHub.Infrastructure.Options.Integration;

/// <summary>
/// Contains reviewed release-time tool contracts; discovery annotations alone cannot authorize a tool.
/// </summary>
public sealed class ExternalMcpOptions
{
    /// <summary>
    /// Gets or sets the exact definitions independently reviewed as read-only.
    /// </summary>
    public List<ReviewedExternalToolOptions> ReviewedTools { get; set; } = [];

    /// <summary>
    /// Gets or sets the private HMAC key for documentation cache fingerprints.
    /// </summary>
    public string DocumentationCacheKey { get; set; } = "";
}
