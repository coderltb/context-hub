namespace ContextHub.Api.Constants;

/// <summary>
/// Composes fixed administration routes from service-owned prefixes.
/// </summary>
public static class AdminRouteConstants
{
    /// <summary>
    /// Identifies the packages route.
    /// </summary>
    public const string PACKAGES = HubConstants.ADMIN_PREFIX + "/packages";

    /// <summary>
    /// Identifies the package versions route.
    /// </summary>
    public const string PACKAGE_VERSIONS = PACKAGES + "/{packageId}/versions";

    /// <summary>
    /// Identifies the catalog search route.
    /// </summary>
    public const string CATALOG_SEARCH = HubConstants.ADMIN_PREFIX + "/catalog/search";

    /// <summary>
    /// Identifies the repositories route.
    /// </summary>
    public const string REPOSITORIES = HubConstants.ADMIN_PREFIX + "/repositories";

    /// <summary>
    /// Identifies the external mcps route.
    /// </summary>
    public const string EXTERNAL_MCPS = HubConstants.ADMIN_PREFIX + "/external-mcps";

    /// <summary>
    /// Identifies the Owner prompt configuration route.
    /// </summary>
    public const string MCP_PROMPTS = HubConstants.ADMIN_PREFIX + "/mcp-prompts";

    /// <summary>
    /// Selects Owner configuration by its database identifier.
    /// </summary>
    public const string PROMPT_ID = "{id:guid}";

    /// <summary>
    /// Identifies the MCP tools route.
    /// </summary>
    public const string MCP_TOOLS = HubConstants.ADMIN_PREFIX + "/mcp-tools";

    /// <summary>
    /// Identifies the recommendations route.
    /// </summary>
    public const string RECOMMENDATIONS = HubConstants.ADMIN_PREFIX + "/recommendations";

    /// <summary>
    /// Identifies the dashboard summary route.
    /// </summary>
    public const string DASHBOARD_SUMMARY = HubConstants.ADMIN_PREFIX + "/dashboard/summary";

    /// <summary>
    /// Identifies the package id route.
    /// </summary>
    public const string PACKAGE_ID = "{packageId}";

    /// <summary>
    /// Identifies the version route.
    /// </summary>
    public const string VERSION = "{version}";

    /// <summary>
    /// Identifies the preview route.
    /// </summary>
    public const string PREVIEW = "preview";

    /// <summary>
    /// Identifies the deprecate route.
    /// </summary>
    public const string DEPRECATE = VERSION + "/deprecate";

    /// <summary>
    /// Identifies the revoke route.
    /// </summary>
    public const string REVOKE = VERSION + "/revoke";

    /// <summary>
    /// Identifies the repository id route.
    /// </summary>
    public const string REPOSITORY_ID = "{repositoryId}";

    /// <summary>
    /// Identifies the sync route.
    /// </summary>
    public const string SYNC = REPOSITORY_ID + "/sync";

    /// <summary>
    /// Identifies the connection name route.
    /// </summary>
    public const string CONNECTION_NAME = "{name}";

    /// <summary>
    /// Identifies the tool name route.
    /// </summary>
    public const string TOOL_NAME = "{toolName}";

    /// <summary>
    /// Identifies the recommendation id route.
    /// </summary>
    public const string RECOMMENDATION_ID = "{recommendationId}";

    /// <summary>
    /// Identifies the recommendation status route.
    /// </summary>
    public const string RECOMMENDATION_STATUS = RECOMMENDATION_ID + "/status";

}
