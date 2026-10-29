namespace ContextHub.Shared.Constants;

/// <summary>
/// Defines transport-independent guard messages for shared data contracts.
/// </summary>
public static class SharedErrorConstants
{
    /// <summary>
    /// Defines safe failures for shared diagnostic masking configuration.
    /// </summary>
    public static class LoggingErrors
    {
        /// <summary>
        /// Rejects a masking budget that would bypass the shared diagnostic bound.
        /// </summary>
        public const string INVALID_JSON_LIMIT = "The JSON masking byte limit must be positive and no larger than 32768.";
    }

    /// <summary>
    /// Messages for page-number pagination contract violations.
    /// </summary>
    public static class PaginationErrors
    {
        /// <summary>
        /// The page must start at one.
        /// </summary>
        public const string PAGE_INVALID = "Page must be greater than or equal to one.";

        /// <summary>
        /// The page size must respect the documented Admin list limit.
        /// </summary>
        public const string PAGE_SIZE_INVALID = "Page size must be between one and one hundred.";

        /// <summary>
        /// The total count cannot be negative.
        /// </summary>
        public const string TOTAL_COUNT_INVALID = "Total count cannot be negative.";

        /// <summary>
        /// A page cannot contain more items than its configured size.
        /// </summary>
        public const string ITEM_COUNT_INVALID = "Item count exceeds the page size.";
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
}
