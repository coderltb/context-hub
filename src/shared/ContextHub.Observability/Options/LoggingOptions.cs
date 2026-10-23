namespace ContextHub.Observability.Options;

/// <summary>
/// Defines startup console rendering and diagnostic thresholds shared by both backend API hosts.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// Gets or sets Text, CompactJson, or Both; only Both adds a text copy on stderr.
    /// </summary>
    public string ConsoleFormat { get; set; } = ObservabilityConstants.COMPACT_JSON;

    /// <summary>
    /// Gets or sets the minimum Serilog level used when the host does not override a category.
    /// </summary>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Gets or sets native Serilog category-prefix overrides without changing the default for other categories.
    /// </summary>
    public Dictionary<string, LogEventLevel> MinimumLevelOverrides { get; set; } = new();
}
