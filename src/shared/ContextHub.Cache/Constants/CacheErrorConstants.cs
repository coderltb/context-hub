namespace ContextHub.Cache.Constants;

/// <summary>
/// Defines safe cache guards without embedding keys or configuration secrets.
/// </summary>
public static class CacheErrorConstants
{
    /// <summary>
    /// Safe errors for coordination configuration.
    /// </summary>
    public static class AtomicErrors
    {
        /// <summary>
        /// Rejects nonpositive lifetimes.
        /// </summary>
        public const string EXPIRATION_INVALID = "The coordination lifetime must be positive.";

        /// <summary>
        /// Requires the host to select its Redis connection without printing its settings.
        /// </summary>
        public const string CONFIGURATION_REQUIRED = "Redis connection configuration is required.";
    }

    /// <summary>
    /// Cache policy and key validation errors.
    /// </summary>
    public static class PolicyErrors
    {
        /// <summary>
        /// Rejects broad invalidation and ambiguous tag identifiers before reaching the native cache.
        /// </summary>
        public const string TAG_INVALID = "Cache tags must be non-empty and cannot contain whitespace or wildcards.";

        /// <summary>
        /// Invalid key namespace message.
        /// </summary>
        public const string NAMESPACE_INVALID =
            "Cache namespace must contain one to sixty-four lowercase letters, digits, dots or hyphens.";

        /// <summary>
        /// Invalid schema version message.
        /// </summary>
        public const string VERSION_INVALID = "Cache schema version must be positive.";

        /// <summary>
        /// Invalid key-part count or length message.
        /// </summary>
        public const string PARTS_INVALID =
            "Cache keys require one to thirty-two trusted parts of at most one thousand twenty-four characters.";

        /// <summary>
        /// Invalid expiration message.
        /// </summary>
        public const string EXPIRATION_INVALID = "Cache expiration must be positive and local expiration cannot exceed shared expiration.";
    }
}
