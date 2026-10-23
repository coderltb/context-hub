namespace ContextHub.Observability.Constants;

/// <summary>
/// Stores bounded defaults and operational property names used by telemetry adapters.
/// </summary>
public static class ObservabilityConstants
{
    /// <summary>
    /// Configuration section for host-selected telemetry signals and instrumentation.
    /// </summary>
    public const string TELEMETRY_SECTION = "Observability:Telemetry";

    /// <summary>
    /// Maximum configured server path exclusions accepted during registration.
    /// </summary>
    public const int MAX_EXCLUDED_PATHS = 32;

    /// <summary>
    /// Maximum length of one configured path segment exclusion.
    /// </summary>
    public const int MAX_EXCLUDED_PATH_LENGTH = 256;

    /// <summary>
    /// Standard HTTP method property name.
    /// </summary>
    public const string REQUEST_METHOD = "RequestMethod";

    /// <summary>
    /// HTTP status code property name.
    /// </summary>
    public const string STATUS_CODE = "StatusCode";

    /// <summary>
    /// Request duration property name, in milliseconds.
    /// </summary>
    public const string ELAPSED = "Elapsed";

    /// <summary>
    /// Fallback for caller-defined HTTP method strings.
    /// </summary>
    public const string UNKNOWN_METHOD = "OTHER";

    /// <summary>
    /// Host logging configuration section.
    /// </summary>
    public const string LOGGING_SECTION = "Observability:Logging";

    /// <summary>
    /// Compact JSON console format name.
    /// </summary>
    public const string COMPACT_JSON = "CompactJson";

    /// <summary>
    /// Development text console format name.
    /// </summary>
    public const string TEXT = "Text";

    /// <summary>
    /// Dual console mode with compact JSON on stdout and a readable copy on stderr.
    /// </summary>
    public const string BOTH = "Both";

    /// <summary>
    /// Readable output template omitting raw exception details.
    /// </summary>
    public const string TEXT_OUTPUT_TEMPLATE = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj} {Properties:j}{NewLine}";

    /// <summary>
    /// Safe exception type property name.
    /// </summary>
    public const string EXCEPTION_TYPE = "ExceptionType";

    /// <summary>
    /// W3C trace identifier property name.
    /// </summary>
    public const string TRACE_ID = "TraceId";

    /// <summary>
    /// W3C span identifier property name.
    /// </summary>
    public const string SPAN_ID = "SpanId";

    /// <summary>
    /// Service identity property name.
    /// </summary>
    public const string SERVICE_NAME = "ServiceName";

    /// <summary>
    /// Environment identity property name.
    /// </summary>
    public const string ENVIRONMENT = "Environment";

    /// <summary>
    /// Maximum host identity length.
    /// </summary>
    public const int MAX_SERVICE_NAME_LENGTH = 128;

    /// <summary>
    /// Content-neutral inbound HTTP span name.
    /// </summary>
    public const string SERVER_OPERATION = "HTTP server request";

    /// <summary>
    /// Content-neutral outbound HTTP span name.
    /// </summary>
    public const string CLIENT_OPERATION = "HTTP client request";

    /// <summary>
    /// Content-neutral application span name.
    /// </summary>
    public const string APPLICATION_OPERATION = "Application operation";

    /// <summary>
    /// Name of the shared application activity source.
    /// </summary>
    public const string ACTIVITY_SOURCE = "ContextHub";
}
