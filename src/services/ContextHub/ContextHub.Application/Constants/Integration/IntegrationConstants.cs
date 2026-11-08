namespace ContextHub.Application.Constants.Integration;

/// <summary>
/// Defines the bounded integration vocabulary shared by use cases and adapters.
/// </summary>
public static class IntegrationConstants
{
    /// <summary>
    /// Identifies a connection that may participate in current routing policy.
    /// </summary>
    public const string ACTIVE = "active";

    /// <summary>
    /// Identifies a retained connection with no routing authority.
    /// </summary>
    public const string DELETED = "deleted";

    /// <summary>
    /// Bounds each managed-search rank branch before reciprocal-rank fusion.
    /// </summary>
    public const int SEARCH_BRANCH_CANDIDATES = 1000;

    /// <summary>
    /// Bounds the managed capabilities returned to the consumer.
    /// </summary>
    public const int SEARCH_RESULT_COUNT = 5;

    /// <summary>
    /// Defines the reciprocal-rank fusion offset.
    /// </summary>
    public const int RRF_RANK_CONSTANT = 60;

    /// <summary>
    /// Identifies an actual lexical metadata match.
    /// </summary>
    public const string KEYWORD_MATCH = "keyword_match";

    /// <summary>
    /// Identifies an actual compatible-vector metadata match.
    /// </summary>
    public const string SEMANTIC_MATCH = "semantic_match";

    /// <summary>
    /// Identifies invalid input without exposing the imported payload.
    /// </summary>
    public const string INVALID_INPUT = "integration_input_invalid";

    /// <summary>
    /// Identifies a missing integration.
    /// </summary>
    public const string NOT_FOUND = "integration_not_found";

    /// <summary>
    /// Identifies an optimistic concurrency conflict.
    /// </summary>
    public const string CONFLICT = "integration_conflict";

    /// <summary>
    /// Defines the safe validation message.
    /// </summary>
    public const string INVALID_MESSAGE = "The integration request is invalid.";

    /// <summary>
    /// Defines the safe missing-resource message.
    /// </summary>
    public const string MISSING_MESSAGE = "The requested integration was not found.";

    /// <summary>
    /// Defines the safe concurrency message.
    /// </summary>
    public const string CONFLICT_MESSAGE = "The integration changed. Reload it before trying again.";

    /// <summary>
    /// Defines the maximum imported JSON size in bytes.
    /// </summary>
    public const int MAX_IMPORT_BYTES = 65_536;

    /// <summary>
    /// Defines the maximum imported connection count.
    /// </summary>
    public const int MAX_CONNECTIONS = 100;

    /// <summary>
    /// Defines the maximum exact source file size.
    /// </summary>
    public const int MAX_SOURCE_BYTES = 1_048_576;

    /// <summary>
    /// Defines the default stable repository ordering.
    /// </summary>
    public const string DEFAULT_REPOSITORY_SORT = "repositoryUrl asc";

    /// <summary>
    /// Lists canonical repository fields accepted by the shared ordering parser.
    /// </summary>
    public static readonly string[] REPOSITORY_SORT_FIELDS = ["repositoryUrl", "lastSyncedAt"];
}
