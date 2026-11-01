namespace ContextHub.Shared.Constants;

/// <summary>
/// Defines shared contract values separately from error messages and log templates.
/// </summary>
public static class SharedConstants
{
    /// <summary>
    /// Defines the supported directions for allowlisted public sort expressions.
    /// </summary>
    public static class SortVariables
    {
        /// <summary>
        /// Identifies ascending ordering and the direction used when it is omitted.
        /// </summary>
        public const string ASCENDING = "asc";

        /// <summary>
        /// Identifies descending ordering for an individual clause.
        /// </summary>
        public const string DESCENDING = "desc";
    }

    /// <summary>
    /// Defines defaults for the shared email options contract.
    /// </summary>
    public static class EmailVariables
    {
        /// <summary>
        /// Bounds send and response buffering together for native SDK requests.
        /// </summary>
        public const int REQUEST_TIMEOUT_SECONDS = 30;
    }

    /// <summary>
    /// Defines one masking vocabulary and budget for application diagnostics and console rendering.
    /// </summary>
    public static class LoggingVariables
    {
        /// <summary>
        /// Names the accepted cross-request correlation value in structured diagnostics.
        /// </summary>
        public const string CORRELATION_ID_PROPERTY = "CorrelationId";

        /// <summary>
        /// Names the host request identifier independently of the correlation header.
        /// </summary>
        public const string REQUEST_ID_PROPERTY = "RequestId";

        /// <summary>
        /// Names the W3C trace identifier in structured diagnostic properties.
        /// </summary>
        public const string TRACE_ID_PROPERTY = "TraceId";

        /// <summary>
        /// Names the W3C span identifier in structured diagnostic properties.
        /// </summary>
        public const string SPAN_ID_PROPERTY = "SpanId";

        /// <summary>
        /// Replaces sensitive values and payloads that cannot be safely inspected.
        /// </summary>
        public const string MASKED_VALUE = "***MASKED***";

        /// <summary>
        /// Bounds diagnostic JSON inspection independently of the larger HTTP response read limit.
        /// </summary>
        public const int MAX_JSON_BYTES = 32 * 1024;

        /// <summary>
        /// Bounds recursion when inspecting external diagnostic JSON.
        /// </summary>
        public const int MAX_JSON_DEPTH = 32;
    }

    /// <summary>
    /// Defines native REST request defaults without coupling shared DTOs to ASP.NET uploaded files.
    /// </summary>
    public static class HttpVariables
    {
        /// <summary>
        /// Names JSON request content.
        /// </summary>
        public const string JSON = "application/json";

        /// <summary>
        /// Names URL-encoded form content.
        /// </summary>
        public const string FORM = "application/x-www-form-urlencoded";

        /// <summary>
        /// Names multipart content whose boundary is supplied by the native HTTP content implementation.
        /// </summary>
        public const string MULTIPART = "multipart/form-data";

        /// <summary>
        /// Defines the default uploaded file field name.
        /// </summary>
        public const string FILE_FIELD = "file";

        /// <summary>
        /// Defines a fallback filename when the integration does not provide one.
        /// </summary>
        public const string FILE_NAME = "upload";
    }

    /// <summary>
    /// Bounds and defaults for ordinary Admin page-number pagination.
    /// </summary>
    public static class PaginationVariables
    {
        /// <summary>
        /// First page in the one-based pagination contract.
        /// </summary>
        public const int FIRST_PAGE = 1;

        /// <summary>
        /// Page size used when the caller does not specify a value.
        /// </summary>
        public const int DEFAULT_PAGE_SIZE = 20;

        /// <summary>
        /// Maximum number of items requested in one Admin list page.
        /// </summary>
        public const int MAX_PAGE_SIZE = 100;
    }

    /// <summary>
    /// Bounds pure identifier generation independently of service uniqueness policy.
    /// </summary>
    public static class CodeVariables
    {
        /// <summary>
        /// Maximum number of cryptographically generated decimal digits.
        /// </summary>
        public const int MAX_LENGTH = 32;

        /// <summary>
        /// Default separator between a caller-owned prefix and numeric suffix.
        /// </summary>
        public const string SEPARATOR = "_";
    }

    /// <summary>
    /// Defines entropy size independently of any service-specific credential format.
    /// </summary>
    public static class TokenVariables
    {
        /// <summary>
        /// Provides two hundred fifty-six bits of cryptographic randomness.
        /// </summary>
        public const int OPAQUE_TOKEN_BYTES = 32;
    }

    /// <summary>
    /// Defines expiration arithmetic defaults independently of token generation and cache policy.
    /// </summary>
    public static class TimeVariables
    {
        /// <summary>
        /// Moves refresh decisions one minute before the real expiration boundary.
        /// </summary>
        public const int EXPIRATION_PADDING_SECONDS = 60;
    }

    /// <summary>
    /// Defines reusable database execution defaults, not service-owned SQL.
    /// </summary>
    public static class DatabaseVariables
    {
        /// <summary>
        /// Limits a default command to thirty seconds.
        /// </summary>
        public const int COMMAND_TIMEOUT_SECONDS = 30;
    }
}
