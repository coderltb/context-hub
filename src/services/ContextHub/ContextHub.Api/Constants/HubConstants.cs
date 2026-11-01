namespace ContextHub.Api.Constants;

/// <summary>
/// Defines the fixed transport and claim vocabulary shared by this host's native adapters.
/// </summary>
public static class HubConstants
{
    /// <summary>
    /// Shares the fixed-window duration with the conservative retry header.
    /// </summary>
    public const int RATE_LIMIT_WINDOW_SECONDS = 60;

    /// <summary>
    /// Keeps requests without a remote address in one bounded ingress partition.
    /// </summary>
    public const string UNKNOWN_REMOTE_ADDRESS = "unknown";

    /// <summary>
    /// Bounds each native introspection backchannel request.
    /// </summary>
    public const int INTROSPECTION_TIMEOUT_SECONDS = 10;

    /// <summary>
    /// Runs dependency status translation after the native challenge status handler.
    /// </summary>
    public const int DEPENDENCY_FAILURE_HANDLER_ORDER_OFFSET = 1_000;

    /// <summary>
    /// Identifies the ingress request-limit setting.
    /// </summary>
    public const string IP_RATE_LIMIT_CONFIGURATION_KEY = "RateLimits:IpRequestsPerMinute";

    /// <summary>
    /// Identifies the authenticated client request-limit setting.
    /// </summary>
    public const string CLIENT_RATE_LIMIT_CONFIGURATION_KEY = "RateLimits:ClientRequestsPerMinute";

    /// <summary>
    /// Defines the default per-address ingress allowance.
    /// </summary>
    public const int DEFAULT_IP_REQUESTS_PER_MINUTE = 120;

    /// <summary>
    /// Defines the default per-client request allowance.
    /// </summary>
    public const int DEFAULT_CLIENT_REQUESTS_PER_MINUTE = 60;

    /// <summary>
    /// Identifies the native CORS policy shared by registration and pipeline.
    /// </summary>
    public const string CORS_POLICY = "ContextHub";

    /// <summary>
    /// Identifies the keyed limiter applied before introspection.
    /// </summary>
    public const string INGRESS_LIMITER = "Ingress";

    /// <summary>
    /// Identifies the keyed limiter applied after client authentication.
    /// </summary>
    public const string CLIENT_LIMITER = "Client";

    /// <summary>
    /// Identifies the approved MCP revision.
    /// </summary>
    public const string PROTOCOL_VERSION = "2026-07-28";

    /// <summary>
    /// Identifies the opaque-token validation scheme registered by OpenIddict.
    /// </summary>
    public const string VALIDATION_SCHEME = "OpenIddict.Validation.AspNetCore";

    /// <summary>
    /// Identifies the human administration policy.
    /// </summary>
    public const string ADMIN_POLICY = "ContextHub.Owner";

    /// <summary>
    /// Identifies the application-token MCP policy.
    /// </summary>
    public const string MCP_POLICY = "ContextHub.Client";

    /// <summary>
    /// Identifies this resource in introspected token audiences.
    /// </summary>
    public const string AUDIENCE = "context-hub-api";

    /// <summary>
    /// Bounds a complete MCP request before parsing.
    /// </summary>
    public const int MAX_REQUEST_BYTES = 65_536;

    /// <summary>
    /// Identifies the administration route boundary.
    /// </summary>
    public const string ADMIN_PREFIX = "api/admin/v1";

    /// <summary>
    /// Shares the absolute Admin boundary between middleware and the REST error adapter.
    /// </summary>
    public const string ADMIN_PATH = "/" + ADMIN_PREFIX;

    /// <summary>
    /// Identifies the single public MCP transport endpoint.
    /// </summary>
    public const string MCP_PATH = "/mcp";

    /// <summary>
    /// Identifies the resource-server configuration section.
    /// </summary>
    public const string AUTHENTICATION_SECTION = "Authentication";

    /// <summary>
    /// Identifies the optimistic concurrency request header.
    /// </summary>
    public const string IF_MATCH_HEADER = "If-Match";

    /// <summary>
    /// Identifies the authorization server protocol prefix.
    /// </summary>
    public const string CONNECT_PREFIX = "/connect";

    /// <summary>
    /// Identifies the native SSO authorization endpoint.
    /// </summary>
    public const string AUTHORIZE_PATH = CONNECT_PREFIX + "/authorize";

    /// <summary>
    /// Identifies the native token exchange endpoint.
    /// </summary>
    public const string TOKEN_PATH = CONNECT_PREFIX + "/token";
}
