namespace ContextHub.Shared.Constants;

/// <summary>
/// Defines shared contract values separately from error messages and log templates.
/// </summary>
public static class SharedConstants
{
    /// <summary>
    /// Defines one masking vocabulary and budget for application diagnostics and console rendering.
    /// </summary>
    public static class LoggingVariables
    {
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
}
