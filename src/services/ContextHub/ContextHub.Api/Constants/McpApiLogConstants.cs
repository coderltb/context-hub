namespace ContextHub.Api.Constants;

/// <summary>
/// Defines metadata-only contract diagnostics.
/// </summary>
public static class McpApiLogConstants
{
    /// <summary>
    /// Reports a rejected output shape without its content.
    /// </summary>
    public const string OUTPUT_REJECTED = "MCP output contract rejected. Tool={ToolName}";

    /// <summary>
    /// Records the failure type without logging stored schemas.
    /// </summary>
    public const string CONTRACT_REJECTED = "MCP stored contract rejected. FailureType={FailureType}";
}
