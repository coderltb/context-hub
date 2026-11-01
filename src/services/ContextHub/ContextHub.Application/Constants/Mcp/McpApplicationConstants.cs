namespace ContextHub.Application.Constants.Mcp;

/// <summary>
/// Keeps the public authentication vocabulary and MCP JSON conventions owned by this service.
/// </summary>
public static class McpApplicationConstants
{
    /// <summary>
    /// Keeps feedback retry metadata aligned across protocol adapters.
    /// </summary>
    public const int SIGNAL_RETRY_AFTER_SECONDS = TelemetryConstants.QUALITY_SIGNAL_WINDOW_SECONDS;

    /// <summary>
    /// Bounds aggregate diagnostics independently from the completed tool outcome.
    /// </summary>
    public const int TELEMETRY_TIMEOUT_SECONDS = 2;

    /// <summary>
    /// Identifies the human administration actor.
    /// </summary>
    public const string USER_ACTOR = "user";

    /// <summary>
    /// Identifies an application using a client credential.
    /// </summary>
    public const string CLIENT_ACTOR = "client";

    /// <summary>
    /// Identifies the actor discriminator supplied by introspection.
    /// </summary>
    public const string ACTOR_TYPE = "actor_type";

    /// <summary>
    /// Identifies the current Owner role.
    /// </summary>
    public const string OWNER_ROLE = "Owner";

    /// <summary>
    /// Identifies the Admin API grant.
    /// </summary>
    public const string ADMIN_SCOPE = "contexthub.admin";

    /// <summary>
    /// Identifies the MCP connection grant.
    /// </summary>
    public const string MCP_SCOPE = "contexthub.mcp";

    /// <summary>
    /// Identifies each exact tool grant.
    /// </summary>
    public const string TOOL_CLAIM = "tool";

    /// <summary>
    /// Identifies the authenticated application independently from a human subject.
    /// </summary>
    public const string CLIENT_ID = "client_id";

    /// <summary>
    /// Formats the approved MCP field names without changing Admin DTO serialization.
    /// </summary>
    public static readonly JsonSerializerOptions JSON_OPTIONS = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        RespectNullableAnnotations = true,
        MaxDepth = 64
    };
}
