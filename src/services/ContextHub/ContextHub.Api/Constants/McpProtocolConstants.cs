namespace ContextHub.Api.Constants;

/// <summary>
/// Defines the host-owned HTTP protocol vocabulary and bounded request-reader settings.
/// </summary>
public static class McpProtocolConstants
{
    /// <summary>
    /// Selects native server options without introducing a second configuration model.
    /// </summary>
    public const string CONFIGURATION_SECTION = "Mcp";

    /// <summary>
    /// Identifies this implementation during native MCP initialization.
    /// </summary>
    public const string SERVER_NAME = "Context Hub";

    /// <summary>
    /// Identifies the implementation independently from the negotiated protocol revision.
    /// </summary>
    public const string SERVER_VERSION = "1.0.0";

    /// <summary>
    /// Identifies the required HTTP protocol revision header.
    /// </summary>
    public const string VERSION_HEADER = "MCP-Protocol-Version";

    /// <summary>
    /// Identifies the HTTP header mirroring the JSON-RPC method.
    /// </summary>
    public const string METHOD_HEADER = "Mcp-Method";

    /// <summary>
    /// Identifies the HTTP header mirroring the selected operation name or resource URI.
    /// </summary>
    public const string NAME_HEADER = "Mcp-Name";

    /// <summary>
    /// Identifies the protocol revision mirrored inside request metadata.
    /// </summary>
    public const string VERSION_METADATA = "io.modelcontextprotocol/protocolVersion";

    /// <summary>
    /// Identifies the JSON-RPC revision in transport rejections.
    /// </summary>
    public const string JSON_RPC_VERSION = "2.0";

    /// <summary>
    /// Bounds each streaming read before the total request-size check.
    /// </summary>
    public const int REQUEST_BUFFER_BYTES = 8192;

    /// <summary>
    /// Bounds JSON nesting before native SDK dispatch.
    /// </summary>
    public const int MAX_REQUEST_DEPTH = 64;
}
