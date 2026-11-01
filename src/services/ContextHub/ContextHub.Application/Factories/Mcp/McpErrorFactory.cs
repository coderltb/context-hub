namespace ContextHub.Application.Factories.Mcp;

/// <summary>
/// Creates the closed safe tool-error branch without exposing exception-owned data.
/// </summary>
public static class McpErrorFactory
{
    /// <summary>
    /// Creates a protocol-neutral business failure using declared codes and messages only.
    /// </summary>
    /// <param name="code">The fixed MCP error identifier.</param>
    /// <param name="message">The safe fixed message.</param>
    /// <param name="retryable">Whether a later attempt may succeed.</param>
    /// <returns>The structured error object used by each tool's output schema.</returns>
    public static JsonObject Create(string code, string message, bool retryable = false) => new()
    {
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
            ["retryable"] = retryable,
            ["details"] = null
        }
    };
}
