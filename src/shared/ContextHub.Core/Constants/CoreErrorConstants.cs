namespace ContextHub.Core.Constants;

/// <summary>
/// Stores Context Hub Core error messages grouped by owning capability.
/// </summary>
public static class CoreErrorConstants
{
    /// <summary>
    /// Defines failures from neutral business exception configuration, not service-specific business vocabulary.
    /// </summary>
    public static class BusinessErrors
    {
        /// <summary>
        /// Rejects undeclared categories before a transport adapter interprets a failure.
        /// </summary>
        public const string KIND_UNSUPPORTED = "The business error kind is not supported.";
    }

    /// <summary>
    /// Safe audit policy failures independent of HTTP authentication.
    /// </summary>
    public static class AuditErrors
    {
        /// <summary>
        /// Rejects an absent actor only when the EF model requires one.
        /// </summary>
        public const string ACTOR_REQUIRED = "An audit actor is required by the entity mapping.";
    }

    /// <summary>
    /// Safe failures from current-user access.
    /// </summary>
    public static class AuthenticationErrors
    {
        /// <summary>
        /// Requires pipeline authentication without disclosing token details.
        /// </summary>
        public const string REQUIRED = "Authentication is required.";

        /// <summary>
        /// Rejects an empty normalized subject mapping during startup.
        /// </summary>
        public const string SUBJECT_CLAIM_REQUIRED = "The normalized subject claim type is required.";
    }

    /// <summary>
    /// Provider-neutral concurrency failures.
    /// </summary>
    public static class ConcurrencyErrors
    {
        /// <summary>
        /// Distinguishes unavailable lock infrastructure from normal contention.
        /// </summary>
        public const string LOCK_UNAVAILABLE = "Distributed lock infrastructure is unavailable.";
    }

    /// <summary>
    /// Defines safe database ownership failures without connection information.
    /// </summary>
    public static class DatabaseErrors
    {
        /// <summary>
        /// Rejects mixing shared persistence contexts in one service container.
        /// </summary>
        public const string PERSISTENCE_CONTEXT_CONFLICT = "Shared persistence is already registered for another database context.";

        /// <summary>
        /// Requires a live caller transaction before borrowing its connection.
        /// </summary>
        public const string TRANSACTION_REQUIRED = "An active database transaction is required.";

        /// <summary>
        /// Rejects a transaction whose connection is closed or detached.
        /// </summary>
        public const string TRANSACTION_CONNECTION_REQUIRED = "The borrowed transaction requires an open connection.";
    }

    /// <summary>
    /// Safe failures from third-party request dispatch and response interpretation.
    /// </summary>
    public static class HttpErrors
    {
        /// <summary>
        /// Rejects an unsupported verb without echoing request content.
        /// </summary>
        public const string METHOD_NOT_SUPPORTED = "The HTTP method is not supported.";

        /// <summary>
        /// Describes a provider failure without disclosing its response or reason phrase.
        /// </summary>
        public const string PROVIDER_FAILED = "The third-party service request failed.";
    }

    /// <summary>
    /// Safe errors from service-owned authorization policies.
    /// </summary>
    public static class AuthorizationErrors
    {
        /// <summary>
        /// Message for an operation that its owning service did not authorize.
        /// </summary>
        public const string DENIED = "The operation is not authorized.";
    }

    /// <summary>
    /// Error messages raised by validation infrastructure.
    /// </summary>
    public static class ValidationErrors
    {
        /// <summary>
        /// Message used when one or more validators reject a request.
        /// </summary>
        public const string FAILURE = "One or more validation errors occurred.";

        /// <summary>
        /// Rejects a negative maximum during collection rule configuration.
        /// </summary>
        public const string MAX_COUNT_INVALID = "The maximum collection count must be nonnegative.";

        /// <summary>
        /// Identifies an optional collection that exceeds its configured inclusive limit.
        /// </summary>
        public const string COLLECTION_TOO_LARGE = "collection_too_large";

        /// <summary>
        /// Describes the limit without including any submitted item values.
        /// </summary>
        public const string COLLECTION_TOO_LARGE_MESSAGE = "{PropertyName} must contain at most {MaxCount} items.";

        /// <summary>
        /// Identifies duplicate collection elements under the selected equality policy.
        /// </summary>
        public const string COLLECTION_DUPLICATES = "collection_duplicates";

        /// <summary>
        /// Describes a duplicate failure without exposing the repeated values.
        /// </summary>
        public const string COLLECTION_DUPLICATES_MESSAGE = "{PropertyName} must contain distinct items.";
    }

    /// <summary>
    /// Safe errors from typed query configuration without field values or search keywords.
    /// </summary>
    public static class QueryErrors
    {
        /// <summary>
        /// Requires explicit valid text fields rather than silently dropping an active search.
        /// </summary>
        public const string TEXT_FIELDS_REQUIRED = "An active text search requires at least one valid string selector.";
    }

    /// <summary>
    /// Error messages raised by generic repository guards.
    /// </summary>
    public static class RepositoryErrors
    {
        /// <summary>
        /// Rejects an offset that native Skip cannot represent before any query executes.
        /// </summary>
        public const string PAGE_OFFSET_TOO_LARGE = "The page offset exceeds the supported query range.";
    }
}
