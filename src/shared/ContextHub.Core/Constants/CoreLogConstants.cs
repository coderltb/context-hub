namespace ContextHub.Core.Constants;

/// <summary>
/// Stores structured log templates used by Context Hub Core.
/// </summary>
public static class CoreLogConstants
{
    /// <summary>
    /// Audit diagnostics that exclude actor and entity identifiers.
    /// </summary>
    public static class AuditLogs
    {
        /// <summary>
        /// Reports the count of opt-in entities stamped at one save boundary.
        /// </summary>
        public const string STAMPED = "Audit save boundary stamped; EntryCount={EntryCount}.";

        /// <summary>
        /// Reports a mapping requirement without disclosing entity content.
        /// </summary>
        public const string ACTOR_REQUIRED = "Audit save rejected because the entity mapping requires an actor.";
    }

    /// <summary>
    /// Safe database command metadata without SQL, parameters or result values.
    /// </summary>
    public static class DatabaseLogs
    {
        /// <summary>
        /// Marks command execution and its connection ownership mode.
        /// </summary>
        public const string STARTED = "Database {Operation} started; BorrowedTransaction={BorrowedTransaction}.";

        /// <summary>
        /// Marks completion without cardinality or data exposure.
        /// </summary>
        public const string COMPLETED = "Database {Operation} completed.";

        /// <summary>
        /// Reports secondary cleanup failure without exposing provider messages or replacing the original command failure.
        /// </summary>
        public const string CLEANUP_FAILED =
            "Database {Operation} cleanup failed; ResourceType={ResourceType}, FailureType={FailureType}.";
    }

    /// <summary>
    /// HTTP metadata and explicitly masked provider diagnostics, excluding raw credentials and exception text.
    /// </summary>
    public static class HttpLogs
    {
        /// <summary>
        /// Reports transport cancellation without exposing the URL or synthesizing a response.
        /// </summary>
        public const string SEND_CANCELLED = "HTTP transport cancelled; Client={ClientName}, Method={Method}.";

        /// <summary>
        /// Reports transport failure type without its credential-bearing exception message.
        /// </summary>
        public const string SEND_FAILED = "HTTP transport failed; Client={ClientName}, Method={Method}, FailureType={FailureType}.";

        /// <summary>
        /// Marks the outbound boundary using only the HTTP method.
        /// </summary>
        public const string SEND_STARTED = "HTTP send started; Method={Method}.";

        /// <summary>
        /// Reports the real response status without reading its content.
        /// </summary>
        public const string SEND_COMPLETED = "HTTP headers received; Method={Method}, StatusCode={StatusCode}.";

        /// <summary>
        /// Identifies a selected internally configured named client without its endpoint or credentials.
        /// </summary>
        public const string CLIENT_SELECTED = "HTTP named client selected; Client={ClientName}.";

        /// <summary>
        /// Records the serialized integration request after masking credential fields.
        /// </summary>
        public const string PROVIDER_STARTED = "Third-party call started; RequestType={RequestType}, MaskedRequest={MaskedRequest}.";

        /// <summary>
        /// Identifies cancellation after headers without disclosing provider body or exception text.
        /// </summary>
        public const string BODY_READ_CANCELLED = "Third-party body read cancelled; RequestType={RequestType}, StatusCode={StatusCode}.";

        /// <summary>
        /// Reports a body consumption failure separately from an upstream HTTP status failure.
        /// </summary>
        public const string BODY_READ_FAILED = "Third-party body read failed; RequestType={RequestType}, StatusCode={StatusCode}, FailureType={FailureType}.";

        /// <summary>
        /// Reports an upstream HTTP failure without exposing unmasked response content or its reason phrase.
        /// </summary>
        public const string PROVIDER_FAILED = "Third-party call failed; RequestType={RequestType}, StatusCode={StatusCode}, MaskedResponse={MaskedResponse}.";

        /// <summary>
        /// Records the same credential-masked response returned by the integration facade.
        /// </summary>
        public const string PROVIDER_COMPLETED = "Third-party call completed; RequestType={RequestType}, StatusCode={StatusCode}, MaskedResponse={MaskedResponse}.";
    }

    /// <summary>
    /// Safe request authorization events, excluding actor and resource identifiers.
    /// </summary>
    public static class AuthorizationLogs
    {
        /// <summary>
        /// Logs a protected request missing its mandatory permission policy.
        /// </summary>
        public const string POLICY_MISSING = "Authorization policy missing for protected request {RequestType}.";

        /// <summary>
        /// Logs a denied request without its payload or detailed policy decision.
        /// </summary>
        public const string DENIED = "Authorization denied for request {RequestType}.";

        /// <summary>
        /// Logs completion of the registered policy checks.
        /// </summary>
        public const string PASSED = "Authorization passed for request {RequestType}; PolicyCount={PolicyCount}.";
    }

    /// <summary>
    /// Validation pipeline log templates.
    /// </summary>
    public static class ValidationLogs
    {
        /// <summary>
        /// Logged before validation without serializing the request.
        /// </summary>
        public const string STARTED = "Validating request {RequestType} with {ValidatorCount} validators.";

        /// <summary>
        /// Logged when validation allows the handler to run.
        /// </summary>
        public const string PASSED = "Validation passed for request {RequestType}.";

        /// <summary>
        /// Logged when validation prevents a request handler from running.
        /// </summary>
        public const string REQUEST_REJECTED =
            "Validation rejected request {RequestType}. FailureCount={FailureCount}, ErrorCodes={ErrorCodes}.";
    }

    /// <summary>
    /// Unit-of-work transaction log templates.
    /// </summary>
    public static class TransactionLogs
    {
        /// <summary>
        /// Records secondary disposal failure without exposing provider details or replacing the original error.
        /// </summary>
        public const string CLEANUP_FAILED =
            "Failed to dispose unit-of-work transaction for database context {DbContextType}. FailureType={FailureType}.";

        /// <summary>
        /// Records explicit tracking cleanup without implying a database rollback.
        /// </summary>
        public const string TRACKER_CLEARED = "Cleared tracked changes for database context {DbContextType}; no save or rollback performed.";

        /// <summary>
        /// Logged before the explicit save boundary.
        /// </summary>
        public const string SAVE_STARTED = "Saving changes for database context {DbContextType}.";

        /// <summary>
        /// Logged after saving, without entity identifiers or field values.
        /// </summary>
        public const string SAVE_COMPLETED = "Saved {EntryCount} state entries for database context {DbContextType}.";

        /// <summary>
        /// Logged when work runs inside a transaction owned by the caller.
        /// </summary>
        public const string OUTER_TRANSACTION_REUSED =
            "Reusing caller-owned transaction for database context {DbContextType}.";

        /// <summary>
        /// Logged when the unit of work starts an owned transaction.
        /// </summary>
        public const string STARTED =
            "Started unit-of-work transaction for database context {DbContextType}.";

        /// <summary>
        /// Logged when the unit of work commits an owned transaction.
        /// </summary>
        public const string COMMITTED =
            "Committed unit-of-work transaction for database context {DbContextType}.";

        /// <summary>
        /// Logged after an owned transaction is rolled back.
        /// </summary>
        public const string ROLLED_BACK =
            "Rolled back unit-of-work transaction for database context {DbContextType}. FailureType={FailureType}.";

        /// <summary>
        /// Logged when rollback itself fails while preserving the original exception.
        /// </summary>
        public const string ROLLBACK_FAILED =
            "Failed to roll back unit-of-work transaction for database context {DbContextType}. FailureType={FailureType}.";
    }

    /// <summary>
    /// Safe repository operation templates, excluding entity identifiers and predicates.
    /// </summary>
    public static class RepositoryLogs
    {
        /// <summary>
        /// Reports projection completion without guessing row existence from a value-type default.
        /// </summary>
        public const string PROJECTION_COMPLETED = "Repository query {Operation} projection completed for entity type {EntityType}.";

        /// <summary>
        /// Logged before a repository query.
        /// </summary>
        public const string QUERY_STARTED = "Repository query {Operation} started for entity type {EntityType}.";

        /// <summary>
        /// Logged after a repository query returns.
        /// </summary>
        public const string QUERY_COMPLETED = "Repository query {Operation} completed for entity type {EntityType}; ResultCount={ResultCount}.";

        /// <summary>
        /// Logged after an operation stages changes without saving.
        /// </summary>
        public const string CHANGE_STAGED = "Repository {Operation} staged for entity type {EntityType}; no save performed.";
    }
}
