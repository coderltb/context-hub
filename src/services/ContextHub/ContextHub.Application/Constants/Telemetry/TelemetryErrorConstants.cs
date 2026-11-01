namespace ContextHub.Application.Constants.Telemetry;

/// <summary>
/// Defines stable telemetry failure codes and safe diagnostics without exposing provider details.
/// </summary>
public static class TelemetryErrorConstants
{
    /// <summary>
    /// Rejects unsupported aggregate dimensions without including submitted values.
    /// </summary>
    public const string INVALID_DIMENSIONS_MESSAGE = "The telemetry dimensions are outside the approved contract.";

    /// <summary>
    /// Identifies the concurrency token invalid outcome.
    /// </summary>
    public const string CONCURRENCY_TOKEN_INVALID = "concurrency_token_invalid";

    /// <summary>
    /// Identifies the package not found outcome.
    /// </summary>
    public const string PACKAGE_NOT_FOUND = "package_not_found";

    /// <summary>
    /// Identifies the quality signal dependency unavailable outcome.
    /// </summary>
    public const string QUALITY_SIGNAL_DEPENDENCY_UNAVAILABLE = "quality_signal_dependency_unavailable";

    /// <summary>
    /// Identifies the quality signal package unavailable outcome.
    /// </summary>
    public const string QUALITY_SIGNAL_PACKAGE_UNAVAILABLE = "quality_signal_package_unavailable";

    /// <summary>
    /// Identifies the quality signal step invalid outcome.
    /// </summary>
    public const string QUALITY_SIGNAL_STEP_INVALID = "quality_signal_step_invalid";

    /// <summary>
    /// Identifies the recommendation conflict outcome.
    /// </summary>
    public const string RECOMMENDATION_CONFLICT = "recommendation_conflict";

    /// <summary>
    /// Identifies the recommendation not found outcome.
    /// </summary>
    public const string RECOMMENDATION_NOT_FOUND = "recommendation_not_found";

    /// <summary>
    /// Identifies the recommendation sort invalid outcome.
    /// </summary>
    public const string RECOMMENDATION_SORT_INVALID = "recommendation_sort_invalid";

    /// <summary>
    /// Identifies the tool name invalid outcome.
    /// </summary>
    public const string TOOL_NAME_INVALID = "tool_name_invalid";

    /// <summary>
    /// Identifies the tool state conflict outcome.
    /// </summary>
    public const string TOOL_STATE_CONFLICT = "tool_state_conflict";
}
