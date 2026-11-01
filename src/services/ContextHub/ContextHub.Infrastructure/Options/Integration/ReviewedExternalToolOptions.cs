namespace ContextHub.Infrastructure.Options.Integration;

/// <summary>
/// Binds reviewed behavior and neutral public metadata to one exact upstream definition.
/// </summary>
public sealed class ReviewedExternalToolOptions
{
    /// <summary>
    /// Gets or sets the Owner-only saved connection name.
    /// </summary>
    public string ConnectionName { get; set; } = "";

    /// <summary>
    /// Gets or sets the exact upstream tool name.
    /// </summary>
    public string ToolName { get; set; } = "";

    /// <summary>
    /// Gets or sets the independently reviewed SHA-256 definition hash.
    /// </summary>
    public string DefinitionHash { get; set; } = "";

    /// <summary>
    /// Gets or sets the provider-neutral public title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the provider-neutral capability summary.
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Gets or sets the bounded neutral result overview.
    /// </summary>
    public string OutputOverview { get; set; } = "";

    /// <summary>
    /// Gets or sets the reviewed documentation role, or null for a general managed tool.
    /// </summary>
    public string? DocumentationRole { get; set; }
}
