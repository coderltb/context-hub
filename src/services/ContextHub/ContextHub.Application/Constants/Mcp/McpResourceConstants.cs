namespace ContextHub.Application.Constants.Mcp;

/// <summary>
/// Owns the fixed resource surface shared by discovery and native URI parsing.
/// </summary>
public static class McpResourceConstants
{
    /// <summary>
    /// Identifies the native JSON resource representation.
    /// </summary>
    public const string MIME_TYPE = "application/json";

    /// <summary>
    /// Gets the five fixed routes; mutable prompt configuration cannot extend this surface.
    /// </summary>
    public static readonly IReadOnlyList<McpResourceDefinitionModel> DEFINITIONS =
    [
        new(
            McpResourceKind.Index,
            "contexthub://catalog/index",
            "Catalog index",
            ToolCatalogConstants.CATALOG_SEARCH),
        new(
            McpResourceKind.Revocations,
            "contexthub://revocations",
            "Revocations",
            ToolCatalogConstants.REVOCATION_CHECK),
        new(
            McpResourceKind.Manifest,
            "contexthub://packages/{package_id}/{version}/manifest",
            "Package manifest",
            ToolCatalogConstants.PACKAGE_GET,
            "manifest"),
        new(
            McpResourceKind.Content,
            "contexthub://packages/{package_id}/{version}/content",
            "Package content",
            ToolCatalogConstants.BUNDLE_RESOLVE,
            "content"),
        new(
            McpResourceKind.Requirements,
            "contexthub://packages/{package_id}/{version}/requirements",
            "Package requirements",
            ToolCatalogConstants.COMPATIBILITY_CHECK,
            "requirements")
    ];
}
