namespace ContextHub.ApiCommon.Constants;

/// <summary>
/// Centralizes approved public HTTP error codes and messages.
/// </summary>
public static class ApiCommonErrorConstants
{
    /// <summary>
    /// Rejects invalid or missing host-owned response boundary configuration.
    /// </summary>
    public static class BoundaryErrors
    {
        /// <summary>
        /// Prevents competing runtime and documentation boundaries within the same host container.
        /// </summary>
        public const string ALREADY_REGISTERED = "Register only one API response boundary per host container.";

        /// <summary>
        /// Requires a non-root segment path without query, fragment or empty segments.
        /// </summary>
        public const string INVALID_PREFIX = "The API route prefix must be a non-root, absolute segment path.";

        /// <summary>
        /// Requires the host to identify its public response version explicitly.
        /// </summary>
        public const string VERSION_REQUIRED = "The API response version must not be blank.";

        /// <summary>
        /// Prevents documentation from assuming a product-specific runtime boundary.
        /// </summary>
        public const string NOT_REGISTERED = "Register the API response boundary before OpenAPI enrichment.";
    }

    /// <summary>
    /// Stores configuration errors that never include serialized example content.
    /// </summary>
    public static class OpenApiErrors
    {
        /// <summary>
        /// Rejects competing registrations for the same mapping key.
        /// </summary>
        public const string DUPLICATE_MAPPING = "OpenAPI mappings must be registered once per key.";

        /// <summary>
        /// Rejects invalid statuses and contradictory operation metadata.
        /// </summary>
        public const string INVALID_CONTRACT = "The OpenAPI operation contract is invalid or ambiguous.";

        /// <summary>
        /// Prevents retaining scoped or singleton fixture providers during document generation.
        /// </summary>
        public const string INVALID_PROVIDER_LIFETIME = "Typed OpenAPI example providers must be transient.";

        /// <summary>
        /// Rejects an attribute fixture incompatible with its native payload schema.
        /// </summary>
        public const string INVALID_EXAMPLE_TYPE = "The OpenAPI example does not match the declared payload type.";

        /// <summary>
        /// Rejects an unregistered provider rather than constructing it through uncontrolled activation.
        /// </summary>
        public const string PROVIDER_NOT_REGISTERED = "The OpenAPI example provider must be registered in dependency injection.";

        /// <summary>
        /// Rejects missing synthetic example values.
        /// </summary>
        public const string EMPTY_EXAMPLE = "The OpenAPI example provider returned no example.";

        /// <summary>
        /// Prevents silent overwriting of distinct response variants.
        /// </summary>
        public const string INVALID_EXAMPLE_NAME = "OpenAPI response example names must be nonempty and unique within a response.";
    }

    /// <summary>
    /// Error messages for request metadata initialization.
    /// </summary>
    public static class MetadataErrors
    {
        /// <summary>
        /// Message for a violated middleware ordering invariant.
        /// </summary>
        public const string NOT_INITIALIZED = "Correlation middleware must run before response metadata is created.";
    }

    /// <summary>
    /// Errors belonging to the API response adapter.
    /// </summary>
    public static class ApiErrors
    {
        /// <summary>
        /// Safe fallback for binding errors whose original value must not be echoed.
        /// </summary>
        public const string INVALID_FIELD = "The field value is invalid.";

        /// <summary>
        /// Guard message for unsupported status mappings.
        /// </summary>
        public const string STATUS_UNSUPPORTED = "The HTTP status has no approved API error mapping.";

        /// <summary>
        /// Machine-readable error_validation code.
        /// </summary>
        public const string VALIDATION_ERROR = "error_validation";

        /// <summary>
        /// Approved English error_validation message.
        /// </summary>
        public const string VALIDATION_ERROR_MESSAGE = "Validation failures have occurred.";

        /// <summary>
        /// Machine-readable error_unauthorized code.
        /// </summary>
        public const string UNAUTHORIZED = "error_unauthorized";

        /// <summary>
        /// Approved English error_unauthorized message.
        /// </summary>
        public const string UNAUTHORIZED_MESSAGE = "Authentication is required.";

        /// <summary>
        /// Machine-readable error_forbidden code.
        /// </summary>
        public const string FORBIDDEN = "error_forbidden";

        /// <summary>
        /// Approved English error_forbidden message.
        /// </summary>
        public const string FORBIDDEN_MESSAGE = "You do not have permission to perform this operation.";

        /// <summary>
        /// Machine-readable error_not_found code.
        /// </summary>
        public const string NOT_FOUND = "error_not_found";

        /// <summary>
        /// Approved English error_not_found message.
        /// </summary>
        public const string NOT_FOUND_MESSAGE = "The requested resource was not found.";

        /// <summary>
        /// Machine-readable error_method_not_allowed code.
        /// </summary>
        public const string METHOD_NOT_ALLOWED = "error_method_not_allowed";

        /// <summary>
        /// Approved English error_method_not_allowed message.
        /// </summary>
        public const string METHOD_NOT_ALLOWED_MESSAGE = "The HTTP method is not supported.";

        /// <summary>
        /// Machine-readable error_not_acceptable code.
        /// </summary>
        public const string NOT_ACCEPTABLE = "error_not_acceptable";

        /// <summary>
        /// Approved English error_not_acceptable message.
        /// </summary>
        public const string NOT_ACCEPTABLE_MESSAGE = "The requested response media type is not supported.";

        /// <summary>
        /// Machine-readable error_conflict code.
        /// </summary>
        public const string CONFLICT = "error_conflict";

        /// <summary>
        /// Approved English error_conflict message.
        /// </summary>
        public const string CONFLICT_MESSAGE = "The operation conflicts with the current resource state.";

        /// <summary>
        /// Machine-readable error_payload_too_large code.
        /// </summary>
        public const string PAYLOAD_TOO_LARGE = "error_payload_too_large";

        /// <summary>
        /// Approved English error_payload_too_large message.
        /// </summary>
        public const string PAYLOAD_TOO_LARGE_MESSAGE = "The request payload is too large.";

        /// <summary>
        /// Machine-readable error_unsupported_media_type code.
        /// </summary>
        public const string UNSUPPORTED_MEDIA_TYPE = "error_unsupported_media_type";

        /// <summary>
        /// Approved English error_unsupported_media_type message.
        /// </summary>
        public const string UNSUPPORTED_MEDIA_TYPE_MESSAGE = "The request media type is not supported.";

        /// <summary>
        /// Machine-readable error_rate_limited code.
        /// </summary>
        public const string RATE_LIMITED = "error_rate_limited";

        /// <summary>
        /// Approved English error_rate_limited message.
        /// </summary>
        public const string RATE_LIMITED_MESSAGE = "The request rate limit was exceeded.";

        /// <summary>
        /// Machine-readable error_internal code.
        /// </summary>
        public const string INTERNAL_ERROR = "error_internal";

        /// <summary>
        /// Approved English error_internal message.
        /// </summary>
        public const string INTERNAL_ERROR_MESSAGE = "An unexpected error occurred.";

        /// <summary>
        /// Machine-readable error_dependency_unavailable code.
        /// </summary>
        public const string DEPENDENCY_UNAVAILABLE = "error_dependency_unavailable";

        /// <summary>
        /// Approved English error_dependency_unavailable message.
        /// </summary>
        public const string DEPENDENCY_UNAVAILABLE_MESSAGE = "A required dependency is unavailable.";
    }
}
