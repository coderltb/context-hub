namespace ContextHub.Infrastructure.Constants.Integration;

/// <summary>
/// Defines stable integration failure codes without exposing provider details.
/// </summary>
public static class IntegrationErrorConstants
{
    /// <summary>
    /// Identifies the credential not allowed outcome.
    /// </summary>
    public const string CREDENTIAL_NOT_ALLOWED = "credential_not_allowed";

    /// <summary>
    /// Identifies the documentation not found outcome.
    /// </summary>
    public const string DOCUMENTATION_NOT_FOUND = "documentation_not_found";

    /// <summary>
    /// Identifies the documentation provider unavailable outcome.
    /// </summary>
    public const string DOCUMENTATION_PROVIDER_UNAVAILABLE = McpErrorConstants.DOCUMENTATION_PROVIDER_UNAVAILABLE;

    /// <summary>
    /// Identifies the external mcp response invalid outcome.
    /// </summary>
    public const string EXTERNAL_MCP_RESPONSE_INVALID = "external_mcp_response_invalid";

    /// <summary>
    /// Identifies the external mcp unavailable outcome.
    /// </summary>
    public const string EXTERNAL_MCP_UNAVAILABLE = "external_mcp_unavailable";

    /// <summary>
    /// Identifies the github response invalid outcome.
    /// </summary>
    public const string GITHUB_RESPONSE_INVALID = "github_response_invalid";

    /// <summary>
    /// Identifies the github unavailable outcome.
    /// </summary>
    public const string GITHUB_UNAVAILABLE = "github_unavailable";

    /// <summary>
    /// Identifies the managed tool definition changed outcome.
    /// </summary>
    public const string MANAGED_TOOL_DEFINITION_CHANGED = "managed_tool_definition_changed";

    /// <summary>
    /// Identifies the managed tool index unavailable outcome.
    /// </summary>
    public const string MANAGED_TOOL_INDEX_UNAVAILABLE = McpErrorConstants.MANAGED_TOOL_INDEX_UNAVAILABLE;

    /// <summary>
    /// Identifies the managed tool input invalid outcome.
    /// </summary>
    public const string MANAGED_TOOL_INPUT_INVALID = McpErrorConstants.MANAGED_TOOL_INPUT_INVALID;

    /// <summary>
    /// Identifies the managed tool not found outcome.
    /// </summary>
    public const string MANAGED_TOOL_NOT_FOUND = "managed_tool_not_found";

    /// <summary>
    /// Identifies the managed tool registry unavailable outcome.
    /// </summary>
    public const string MANAGED_TOOL_REGISTRY_UNAVAILABLE = McpErrorConstants.MANAGED_TOOL_REGISTRY_UNAVAILABLE;

    /// <summary>
    /// Identifies the managed tool response invalid outcome.
    /// </summary>
    public const string MANAGED_TOOL_RESPONSE_INVALID = "managed_tool_response_invalid";

    /// <summary>
    /// Identifies the managed tool unavailable outcome.
    /// </summary>
    public const string MANAGED_TOOL_UNAVAILABLE = McpErrorConstants.MANAGED_TOOL_UNAVAILABLE;

    /// <summary>
    /// Identifies the unsafe endpoint outcome.
    /// </summary>
    public const string UNSAFE_ENDPOINT = "unsafe_endpoint";

    /// <summary>
    /// Identifies the unsupported transport outcome.
    /// </summary>
    public const string UNSUPPORTED_TRANSPORT = "unsupported_transport";

    /// <summary>
    /// Identifies the validation error outcome.
    /// </summary>
    public const string VALIDATION_ERROR = McpErrorConstants.VALIDATION_ERROR;
}
