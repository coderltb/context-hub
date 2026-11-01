namespace ContextHub.Application.Constants.Mcp;

/// <summary>
/// Defines safe diagnostics for tool dispatch without request or result payloads.
/// </summary>
public static class McpLogConstants
{
    /// <summary>
    /// Records a rejected business operation using only its tool and classification.
    /// </summary>
    public const string BUSINESS_REJECTED = "MCP business operation rejected. Tool={ToolName} Kind={FailureKind}";

    /// <summary>
    /// Reports diagnostic failure without replacing the completed tool outcome.
    /// </summary>
    public const string TELEMETRY_FAILED = "MCP aggregate could not be recorded. FailureType={FailureType}";
}

