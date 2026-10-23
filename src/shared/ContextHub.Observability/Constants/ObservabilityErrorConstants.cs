namespace ContextHub.Observability.Constants;

/// <summary>
/// Stores configuration errors without including submitted values or credentials.
/// </summary>
public static class ObservabilityErrorConstants
{
    /// <summary>
    /// Safe message for invalid or unbounded telemetry path exclusions.
    /// </summary>
    public const string INVALID_EXCLUDED_PATHS = "Telemetry path exclusions must be bounded non-root absolute paths without query or fragment components.";

    /// <summary>
    /// Message for an unsupported stdout formatter selection.
    /// </summary>
    public const string INVALID_CONSOLE_FORMAT = "ConsoleFormat must be Text, CompactJson or Both.";

    /// <summary>
    /// Message for a non-existent Serilog minimum level.
    /// </summary>
    public const string INVALID_LOG_LEVEL = "MinimumLevel must be a supported Serilog level.";

    /// <summary>
    /// Message for invalid category names or category-level thresholds.
    /// </summary>
    public const string INVALID_LOG_LEVEL_OVERRIDES = "MinimumLevelOverrides must contain non-empty categories and supported Serilog levels.";

    /// <summary>
    /// Message for an invalid logging service name.
    /// </summary>
    public const string INVALID_SERVICE_NAME = "A bounded, non-empty service name is required.";
}
