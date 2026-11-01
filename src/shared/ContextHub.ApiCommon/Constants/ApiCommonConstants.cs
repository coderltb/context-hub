namespace ContextHub.ApiCommon.Constants;

/// <summary>
/// Defines shared HTTP metadata identifiers, separate from business and MCP contracts.
/// </summary>
public static class ApiCommonConstants
{
    /// <summary>
    /// Defines optional self-declared device headers independently of authentication.
    /// </summary>
    public static class ClientDeviceHeaders
    {
        /// <summary>
        /// Identifies the optional caller-declared device identifier.
        /// </summary>
        public const string DEVICE_ID = "X-Device-Id";

        /// <summary>
        /// Identifies the optional caller-declared device name.
        /// </summary>
        public const string DEVICE_NAME = "X-Device-Name";

        /// <summary>
        /// Identifies the optional caller-declared device category.
        /// </summary>
        public const string DEVICE_TYPE = "X-Device-Type";
    }

    /// <summary>
    /// Positions API error adaptation before ASP.NET Core's built-in client-error result filter at order -2000.
    /// </summary>
    public const int API_ERROR_FILTER_ORDER = -2001;

    /// <summary>
    /// Places DTO logging outside API adaptation (-2001) and native automatic validation (-2000).
    /// </summary>
    public const int API_LOGGING_FILTER_ORDER = -2002;

    /// <summary>
    /// Defines diagnostic payload limits and metadata-only omission classifications.
    /// </summary>
    public static class Logging
    {
        /// <summary>
        /// Limits each redacted diagnostic string without truncating JSON or changing the response.
        /// </summary>
        public const int MAX_PAYLOAD_LENGTH = 16_384;

        /// <summary>
        /// Identifies an omitted multipart body whose files remain transport-owned.
        /// </summary>
        public const string MULTIPART = "multipart";

        /// <summary>
        /// Identifies an endpoint whose payload is deliberately excluded from diagnostics.
        /// </summary>
        public const string METADATA_ONLY = "metadata_only";

        /// <summary>
        /// Identifies a transport, binary or deferred value excluded from DTO serialization.
        /// </summary>
        public const string UNSUPPORTED = "unsupported_payload";

        /// <summary>
        /// Identifies a masked payload exceeding the diagnostic limit.
        /// </summary>
        public const string SIZE_LIMIT = "size_limit";

        /// <summary>
        /// Identifies a diagnostic JSON failure without exposing the original object or exception text.
        /// </summary>
        public const string SERIALIZATION_FAILURE = "serialization_failure";
    }

    /// <summary>
    /// Content type for the public API JSON envelope.
    /// </summary>
    public const string JSON_CONTENT_TYPE = SharedConstants.HttpVariables.JSON;

    /// <summary>
    /// Canonical correlation header and request item key.
    /// </summary>
    public const string CORRELATION_ID = "X-Correlation-ID";

    /// <summary>
    /// Request item holding the accepted UTC timestamp.
    /// </summary>
    public const string REQUEST_TIMESTAMP = "ContextHub.RequestTimestamp";

    /// <summary>
    /// Request item holding the active W3C activity.
    /// </summary>
    public const string REQUEST_ACTIVITY = "ContextHub.RequestActivity";

    /// <summary>
    /// Activity name used only when the host did not create an activity.
    /// </summary>
    public const string HTTP_ACTIVITY = "ContextHub.HttpRequest";

    /// <summary>
    /// Identifies multipart content whose body must remain excluded from DTO payload diagnostics.
    /// </summary>
    public const string MULTIPART_MEDIA_PREFIX = "multipart/";
}
