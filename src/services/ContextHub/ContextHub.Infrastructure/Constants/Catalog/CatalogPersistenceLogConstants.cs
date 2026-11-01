namespace ContextHub.Infrastructure.Constants.Catalog;

/// <summary>
/// Defines metadata-only catalog diagnostics.
/// </summary>
public static class CatalogPersistenceLogConstants
{
    /// <summary>
    /// Reports an unavailable immutable-content cache without recording source or credentials.
    /// </summary>
    public const string IMMUTABLE_CACHE_UNAVAILABLE = "Immutable catalog cache unavailable. FailureType={FailureType}";
}

