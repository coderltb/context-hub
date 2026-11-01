namespace ContextHub.Api.Constants;

/// <summary>
/// Defines safe protocol failures without stored schema or provider details.
/// </summary>
public static class McpApiErrorConstants
{
    /// <summary>
    /// Rejects an unapproved browser origin without echoing it.
    /// </summary>
    public const string ORIGIN_NOT_PERMITTED = "The request origin is not permitted.";

    /// <summary>
    /// Rejects a transport method or revision outside the supported contract.
    /// </summary>
    public const string UNSUPPORTED_PROTOCOL_VERSION = "Unsupported protocol version.";

    /// <summary>
    /// Rejects declared and chunked bodies exceeding the same request limit.
    /// </summary>
    public const string REQUEST_TOO_LARGE = "Request exceeds the size limit.";

    /// <summary>
    /// Rejects malformed JSON without returning parser details.
    /// </summary>
    public const string INVALID_JSON = "Invalid JSON request.";

    /// <summary>
    /// Rejects body metadata that disagrees with the HTTP routing headers.
    /// </summary>
    public const string METADATA_MISMATCH = "Protocol metadata does not match.";

    /// <summary>
    /// Rejects a non-string operation identifier before authorization dispatch.
    /// </summary>
    public const string INVALID_OPERATION_IDENTIFIER = "The operation identifier is invalid.";

    /// <summary>
    /// Rejects an operation name that disagrees with its routing header.
    /// </summary>
    public const string OPERATION_NAME_MISMATCH = "Operation name does not match.";

    /// <summary>
    /// Rejects unknown resource URIs without exposing internal routing.
    /// </summary>
    public const string RESOURCE_UNAVAILABLE = "The resource is not available.";

    /// <summary>
    /// Identifies a missing authorized tool.
    /// </summary>
    public const string TOOL_UNAVAILABLE = "The tool is not available.";

    /// <summary>
    /// Describes a stored contract that cannot be evaluated safely.
    /// </summary>
    public const string CONTRACT_UNAVAILABLE = "The tool contract is unavailable.";

    /// <summary>
    /// Describes a result that cannot satisfy the published contract.
    /// </summary>
    public const string OUTPUT_UNAVAILABLE = McpErrorConstants.RESULT_UNAVAILABLE;
}
