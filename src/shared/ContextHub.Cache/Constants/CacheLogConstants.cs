namespace ContextHub.Cache.Constants;

/// <summary>
/// Stores safe structured cache log templates.
/// </summary>
public static class CacheLogConstants
{
    /// <summary>
    /// Safe coordination diagnostics without raw keys or values.
    /// </summary>
    public static class AtomicLogs
    {
        /// <summary>
        /// Reports a completed operation without its data.
        /// </summary>
        public const string COMPLETED = "Atomic cache {Operation} completed.";

        /// <summary>
        /// Reports provider failure without connection settings or messages.
        /// </summary>
        public const string FAILED = "Coordination {Operation} failed; FailureType={FailureType}.";

        /// <summary>
        /// Distinguishes lock contention from successful acquisition.
        /// </summary>
        public const string LOCK_COMPLETED = "Distributed lock attempt completed; Acquired={Acquired}.";
    }

    /// <summary>
    /// Templates for exact-key cache operations.
    /// </summary>
    public static class EntryLogs
    {
        /// <summary>
        /// Records logical invalidation without emitting caller-supplied tag identifiers.
        /// </summary>
        public const string TAG_REMOVED = "Cache tag group logically invalidated.";

        /// <summary>
        /// Logs the start of a cache operation without revealing key parts.
        /// </summary>
        public const string STARTED = "Cache {Operation} started in namespace {CacheNamespace}.";

        /// <summary>
        /// Logs the completed read, without claiming whether other callers coalesced or hit the cache.
        /// </summary>
        public const string RESOLVED = "Cache value resolved in namespace {CacheNamespace}.";

        /// <summary>
        /// Logs a source read only when HybridCache actually invokes the factory.
        /// </summary>
        public const string SOURCE_READ = "Cache factory reading source for namespace {CacheNamespace}.";

        /// <summary>
        /// Logs a successful cache write without serializing the value.
        /// </summary>
        public const string STORED = "Cache value stored in namespace {CacheNamespace}.";

        /// <summary>
        /// Removal log deliberately excludes raw key parts and cached content.
        /// </summary>
        public const string REMOVED = "Cache entry removed from namespace {CacheNamespace}.";
    }
}
