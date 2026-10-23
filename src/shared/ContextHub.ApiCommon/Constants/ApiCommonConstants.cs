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
    /// Content type for the public API JSON envelope.
    /// </summary>
    public const string JSON_CONTENT_TYPE = "application/json";

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
}
