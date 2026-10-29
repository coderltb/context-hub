namespace ContextHub.Observability.Options;

/// <summary>
/// Defines startup console rendering and diagnostic thresholds shared by both backend API hosts.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class LoggingOptions
{
    /// <summary>
    /// Gets or sets the native configuration-bound rendering mode; JSON settings keep Text, CompactJson or Both names.
    /// </summary>
    public ConsoleLogFormat ConsoleFormat { get; set; } = ConsoleLogFormat.CompactJson;

    /// <summary>
    /// Gets or sets the minimum Serilog level used when the host does not override a category.
    /// </summary>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Gets or sets raw category-prefix overrides; null configuration is rejected during startup validation.
    /// </summary>
    public Dictionary<string, LogEventLevel>? MinimumLevelOverrides { get; set; } = new();
}
