namespace ContextHub.Application.Constants.Mcp;

/// <summary>
/// Owns executable handler names, security annotations and discovery order; metadata and contracts come from PostgreSQL.
/// </summary>
public static class ToolCatalogConstants
{
    /// <summary>
    /// Names the catalog search capability.
    /// </summary>
    public const string CATALOG_SEARCH = "catalog_search";

    /// <summary>
    /// Names the library documentation search capability.
    /// </summary>
    public const string LIBRARY_DOCUMENTATION_SEARCH = "library_documentation_search";

    /// <summary>
    /// Names the package get capability.
    /// </summary>
    public const string PACKAGE_GET = "package_get";

    /// <summary>
    /// Names the bundle resolve capability.
    /// </summary>
    public const string BUNDLE_RESOLVE = "bundle_resolve";

    /// <summary>
    /// Names the compatibility check capability.
    /// </summary>
    public const string COMPATIBILITY_CHECK = "compatibility_check";

    /// <summary>
    /// Names the revocation check capability.
    /// </summary>
    public const string REVOCATION_CHECK = "revocation_check";

    /// <summary>
    /// Names the managed tool search capability.
    /// </summary>
    public const string MANAGED_TOOL_SEARCH = "managed_tool_search";

    /// <summary>
    /// Names the managed tool describe capability.
    /// </summary>
    public const string MANAGED_TOOL_DESCRIBE = "managed_tool_describe";

    /// <summary>
    /// Names the managed tool call capability.
    /// </summary>
    public const string MANAGED_TOOL_CALL = "managed_tool_call";

    /// <summary>
    /// Names the quality signal submit capability.
    /// </summary>
    public const string QUALITY_SIGNAL_SUBMIT = "quality_signal_submit";

    /// <summary>
    /// Gets the explicitly registered capability definitions.
    /// </summary>
    public static readonly IReadOnlyList<ToolDefinitionModel> TOOLS = Array.AsReadOnly(new[]
    {
        new ToolDefinitionModel {
            Name = CATALOG_SEARCH,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = PACKAGE_GET,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = BUNDLE_RESOLVE,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = COMPATIBILITY_CHECK,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = REVOCATION_CHECK,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = LIBRARY_DOCUMENTATION_SEARCH,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = MANAGED_TOOL_SEARCH,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = MANAGED_TOOL_DESCRIBE,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = MANAGED_TOOL_CALL,
            ReadOnly = true
        },
        new ToolDefinitionModel {
            Name = QUALITY_SIGNAL_SUBMIT,
            ReadOnly = false
        },
    });
}
