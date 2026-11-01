namespace ContextHub.Application.Constants.Telemetry;

/// <summary>
/// Names fixed V1 workflow values and privacy-safe diagnostics.
/// </summary>
public static class TelemetryConstants
{
    /// <summary>
    /// Defines the shared fixed-hour budget and its MCP retry metadata.
    /// </summary>
    public const int QUALITY_SIGNAL_WINDOW_SECONDS = 3600;

    /// <summary>
    /// Identifies a newly persisted client signal.
    /// </summary>
    public const string SIGNAL_ACCEPTED = "accepted";

    /// <summary>
    /// Identifies a signal merged into an existing review item.
    /// </summary>
    public const string SIGNAL_DEDUPLICATED = "deduplicated";

    /// <summary>
    /// Bounds self-declared client metadata independently from authentication and authorization.
    /// </summary>
    public const int MAX_CLIENT_FAMILY_LENGTH = 64;

    /// <summary>
    /// Identifies a recommendation awaiting review.
    /// </summary>
    public const string STATUS_NEW = "new";

    /// <summary>
    /// Identifies an accepted recommendation awaiting completion.
    /// </summary>
    public const string STATUS_ACCEPTED = "accepted";

    /// <summary>
    /// Identifies a dismissed recommendation.
    /// </summary>
    public const string STATUS_DISMISSED = "dismissed";

    /// <summary>
    /// Identifies a completed recommendation.
    /// </summary>
    public const string STATUS_COMPLETED = "completed";

    /// <summary>
    /// Identifies a package improvement recommendation.
    /// </summary>
    public const string PACKAGE_IMPROVEMENT = "package_improvement";

    /// <summary>
    /// Identifies a compatibility recommendation.
    /// </summary>
    public const string COMPATIBILITY_GAP = "compatibility_gap";

    /// <summary>
    /// Identifies a search coverage recommendation.
    /// </summary>
    public const string SEARCH_GAP = "search_gap";

    /// <summary>
    /// Identifies a recommendation submitted by an Owner.
    /// </summary>
    public const string OWNER_ORIGIN = "owner";

    /// <summary>
    /// Bounds each dashboard ranking list.
    /// </summary>
    public const int DASHBOARD_TOP_COUNT = 10;

    /// <summary>
    /// Lists categories an Owner may create manually.
    /// </summary>
    public static readonly string[] MANUAL_TYPES = [PACKAGE_IMPROVEMENT, COMPATIBILITY_GAP, SEARCH_GAP];

    /// <summary>
    /// Lists supported review workflow states.
    /// </summary>
    public static readonly string[] STATUSES = [STATUS_NEW, STATUS_ACCEPTED, STATUS_DISMISSED, STATUS_COMPLETED];

    /// <summary>
    /// Bounds reports per application in one fixed hour.
    /// </summary>
    public const int QUALITY_SIGNALS_PER_HOUR = 10;

    /// <summary>
    /// Requires enough daily observations before opening reliability feedback.
    /// </summary>
    public const int RELIABILITY_MINIMUM_COUNT = 20;

    /// <summary>
    /// Defines the daily failure ratio that opens reliability feedback.
    /// </summary>
    public const double RELIABILITY_FAILURE_RATIO = 0.2;

    /// <summary>
    /// Describes an optimistic conflict without exposing persisted state.
    /// </summary>
    public const string CONFLICT_MESSAGE = "The resource changed; reload it before retrying.";

    /// <summary>
    /// Describes a missing resource safely.
    /// </summary>
    public const string NOT_FOUND_MESSAGE = "The requested resource was not found.";
}
