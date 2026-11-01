namespace ContextHub.Infrastructure.Constants.Catalog;

/// <summary>
/// Identifies native database constraints used to classify catalog conflicts.
/// </summary>
public static class CatalogPersistenceConstants
{
    /// <summary>
    /// Identifies uniqueness of a public package identity.
    /// </summary>
    public const string PACKAGE_KEY_CONSTRAINT = "UQ_Catalog_Package_Key";

    /// <summary>
    /// Identifies uniqueness of an exact release within a package.
    /// </summary>
    public const string VERSION_NUMBER_CONSTRAINT = "UQ_Catalog_Version_PackageId_Number";
}
