namespace ContextHub.Infrastructure.Constants;

/// <summary>
/// Defines service-owned configuration identifiers without duplicating native options.
/// </summary>
public static class InfrastructureConstants
{
    /// <summary>
    /// Selects this service's named PostgreSQL connection.
    /// </summary>
    public const string DATABASE_CONNECTION = "ContextHub";

    /// <summary>
    /// Selects native cache defaults supplied by the host.
    /// </summary>
    public const string CACHE_SETTINGS = "CacheSettings";

    /// <summary>
    /// Selects the existing Redis connection settings.
    /// </summary>
    public const string REDIS_SETTINGS = CACHE_SETTINGS + ":RedisSettings";

    /// <summary>
    /// Isolates this service's distributed cache keys.
    /// </summary>
    public const string CACHE_PREFIX = "context-hub:";

    /// <summary>
    /// Isolates protected cursor payloads from Authentication credentials.
    /// </summary>
    public const string DATA_PROTECTION_APPLICATION = "ContextHub.ContextHub";

    /// <summary>
    /// Binds protected search continuations to their existing format and purpose.
    /// </summary>
    public const string CATALOG_CURSOR_PROTECTION_PURPOSE = "ContextHub.Catalog.SearchCursor.v1";

    /// <summary>
    /// Selects the deployment-owned persistent key ring path.
    /// </summary>
    public const string KEY_RING_PATH = "DataProtection:KeyRingPath";

    /// <summary>
    /// Selects optional local inference settings.
    /// </summary>
    public const string EMBEDDING_SETTINGS = "Embedding";

    /// <summary>
    /// Identifies the native database connectivity check.
    /// </summary>
    public const string DATABASE_HEALTH_CHECK = "postgresql";

    /// <summary>
    /// Selects database readiness independently from optional upstream services.
    /// </summary>
    public const string READINESS_TAG = "ready";
}
