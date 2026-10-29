namespace ContextHub.Cache.Constants;

/// <summary>
/// Defines cache key and payload limits separately from errors and logging.
/// </summary>
public static class CacheConstants
{
    /// <summary>
    /// Defines bounded memory coordination and the Redis atomic counter script.
    /// </summary>
    public static class AtomicVariables
    {
        /// <summary>
        /// Triggers cleanup every sixty-four operations.
        /// </summary>
        public const int SWEEP_INTERVAL = 64;

        /// <summary>
        /// Creates a counter and its initial expiry in one server operation without extending existing TTL.
        /// </summary>
        public const string INCREMENT_WITH_EXPIRATION = "local created = redis.call('EXISTS', KEYS[1]) == 0; local value = redis.call('INCR', KEYS[1]); if created then redis.call('PEXPIRE', KEYS[1], ARGV[1]); end; return value;";
    }

    /// <summary>
    /// Values defining the bounded, versioned cache-key format.
    /// </summary>
    public static class KeyVariables
    {
        /// <summary>
        /// Prefix separating Context Hub entries from other applications.
        /// </summary>
        public const string PREFIX = "context-hub";

        /// <summary>
        /// Maximum length of a trusted capability namespace.
        /// </summary>
        public const int MAX_NAMESPACE_LENGTH = 64;

        /// <summary>
        /// Maximum number of identifying values in one key.
        /// </summary>
        public const int MAX_PARTS = 32;

        /// <summary>
        /// Maximum length of each identifying value before hashing.
        /// </summary>
        public const int MAX_PART_LENGTH = 1024;
    }

    /// <summary>
    /// Defaults passed to the native cache implementation.
    /// </summary>
    public static class HybridCacheVariables
    {
        /// <summary>
        /// Supplies the shared entry lifetime only when the host has not selected one.
        /// </summary>
        public const int DEFAULT_EXPIRATION_SECONDS = 60;

        /// <summary>
        /// Bounds local staleness independently of the shared entry lifetime.
        /// </summary>
        public const int DEFAULT_LOCAL_EXPIRATION_SECONDS = 15;

        /// <summary>
        /// Maximum serialized payload size in bytes.
        /// </summary>
        public const int MAX_PAYLOAD_BYTES = 1024 * 1024;

        /// <summary>
        /// Maximum full cache-key length in characters.
        /// </summary>
        public const int MAX_KEY_LENGTH = 256;
    }
}
