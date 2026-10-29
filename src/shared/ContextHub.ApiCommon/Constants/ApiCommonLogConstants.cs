namespace ContextHub.ApiCommon.Constants;

/// <summary>
/// Stores structured log templates owned by the HTTP adapter.
/// </summary>
public static class ApiCommonLogConstants
{
    /// <summary>
    /// Records redacted MVC DTO diagnostics separately from native HTTP completion and business events.
    /// </summary>
    public static class PayloadLogs
    {
        /// <summary>
        /// Records bound arguments only after redaction or an explicit metadata-only omission.
        /// </summary>
        public const string REQUEST_RECEIVED = "API request received by {Action}; payload {RequestPayload}; omitted {PayloadOmittedReason}; content length {ContentLength}.";

        /// <summary>
        /// Records the executed response and final status after all inner response adapters completed.
        /// </summary>
        public const string RESPONSE_PRODUCED = "API response produced by {Action}; status {StatusCode}; payload {ResponsePayload}; omitted {PayloadOmittedReason}.";

        /// <summary>
        /// Records a diagnostic serializer failure without exception messages or the rejected object.
        /// </summary>
        public const string SERIALIZATION_FAILED = "API diagnostic serialization failed with {ExceptionType}; payload omitted.";
    }

    /// <summary>
    /// Current-user boundary metadata without subject identifiers or claim values.
    /// </summary>
    public static class AuthenticationLogs
    {
        /// <summary>
        /// Records an anonymous request to an authenticated operation.
        /// </summary>
        public const string REQUIRED = "Authenticated identity required for this operation.";
    }

    /// <summary>
    /// Stores documentation diagnostics without logging fixtures or serialized payloads.
    /// </summary>
    public static class OpenApiLogs
    {
        /// <summary>
        /// Records mapping counts and justified statuses without exposing documentation payloads.
        /// </summary>
        public const string MAPPINGS_APPLIED = "OpenAPI mappings: {RequestCount} requests, {ResponseCount} responses, {OperationCount} contracts; {ErrorCount} error statuses.";

        /// <summary>
        /// Records the number of JSON media entries enriched during document generation.
        /// </summary>
        public const string EXAMPLES_APPLIED = "API OpenAPI examples applied; MediaCount={MediaCount}.";
    }

    /// <summary>
    /// Stores safe transport adaptation diagnostics without request paths or submitted values.
    /// </summary>
    public static class ResponseLogs
    {
        /// <summary>
        /// Records conversion of a native controller status to the approved API envelope.
        /// </summary>
        public const string NATIVE_ERROR_MAPPED = "Native controller error adapted to API envelope; StatusCode={StatusCode}.";
    }

    /// <summary>
    /// Safe correlation and request metadata log templates.
    /// </summary>
    public static class CorrelationLogs
    {
        /// <summary>
        /// Records removal of unsafe outbound metadata without including its submitted value.
        /// </summary>
        public const string OUTBOUND_HEADER_REMOVED = "Malformed outbound correlation header removed.";

        /// <summary>
        /// Records successful propagation without exposing destination or request payload.
        /// </summary>
        public const string OUTBOUND_PROPAGATED = "Validated correlation identifier propagated to the outbound request.";

        /// <summary>
        /// Logs accepted request metadata, excluding raw header and query input.
        /// </summary>
        public const string INITIALIZED =
            "Request metadata initialized; RequestId={RequestId}, CorrelationId={CorrelationId}, HeaderPreserved={HeaderPreserved}.";

        /// <summary>
        /// Logs malformed header replacement without logging the original value.
        /// </summary>
        public const string HEADER_REPLACED = "Invalid correlation header replaced for request {RequestId}.";

        /// <summary>
        /// Logs that downstream processing returned, not a duplicate HTTP completion event.
        /// </summary>
        public const string DOWNSTREAM_RETURNED = "Request pipeline returned; RequestId={RequestId}, StatusCode={StatusCode}.";
    }

    /// <summary>
    /// Safe exception-handling log templates.
    /// </summary>
    public static class ExceptionLogs
    {
        /// <summary>
        /// Records declared failure metadata without publishing exception messages or payloads.
        /// </summary>
        public const string EXPECTED_FAILURE = "API exception {ExceptionType}; status {StatusCode}; code {ErrorCode}; request {RequestId}.";

        /// <summary>
        /// Logs validation rejection using counts rather than messages or submitted field values.
        /// </summary>
        public const string VALIDATION_REJECTED = "API request {RequestId} rejected with {FieldCount} invalid fields.";

        /// <summary>
        /// Logs the type and request identifier, excluding exception messages, stack traces and payloads.
        /// </summary>
        public const string UNEXPECTED_FAILURE = "Unhandled API exception {ExceptionType}; request {RequestId}.";
    }
}
