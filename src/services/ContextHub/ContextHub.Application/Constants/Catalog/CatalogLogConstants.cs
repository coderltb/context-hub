namespace ContextHub.Application.Constants.Catalog;

/// <summary>
/// Records committed catalog lifecycle events without logging package bodies or source provenance.
/// </summary>
public static class CatalogLogConstants
{
    /// <summary>
    /// Records a completed publication decision, including idempotent requests for an existing immutable release.
    /// </summary>
    public const string PUBLICATION_ACCEPTED = "Catalog publication accepted. PackageId={PackageId} Version={Version}";

    /// <summary>
    /// Records a committed terminal revocation after the persistence boundary returns successfully.
    /// </summary>
    public const string VERSION_REVOKED = "Catalog release revoked. PackageId={PackageId} Version={Version}";
}
