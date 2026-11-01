namespace ContextHub.Infrastructure.Constants.Integration;

/// <summary>
/// Names the integration-owned settings, transport and safe diagnostic vocabulary.
/// </summary>
public static class IntegrationInfrastructureConstants
{
    /// <summary>
    /// Bounds source documents inspected during one request-bound GitHub sync.
    /// </summary>
    public const int MAX_INSPECTED_SOURCE_FILES = 100;

    /// <summary>
    /// Requires the exact full Git commit identifier used by the current authoring contract.
    /// </summary>
    public const int COMMIT_SHA_LENGTH = 40;

    /// <summary>
    /// Identifies a Git symlink that must not be followed while inspecting source.
    /// </summary>
    public const string GIT_SYMLINK_MODE = "120000";

    /// <summary>
    /// Allows provider clock skew when creating a short-lived GitHub App assertion.
    /// </summary>
    public const int APP_ASSERTION_CLOCK_SKEW_SECONDS = 60;

    /// <summary>
    /// Keeps the App assertion within the provider's short-lived credential contract.
    /// </summary>
    public const int APP_ASSERTION_LIFETIME_MINUTES = 9;

    /// <summary>
    /// Expires the cached installation credential before its provider expiration.
    /// </summary>
    public const int INSTALLATION_TOKEN_REFRESH_MARGIN_MINUTES = 1;

    /// <summary>
    /// Bounds the neutral documentation payload.
    /// </summary>
    public const int MAX_DOCUMENTATION_RESPONSE_BYTES = 49_152;

    /// <summary>
    /// Identifies the reviewed absence of side effects.
    /// </summary>
    public const string NO_SIDE_EFFECT = "none";

    /// <summary>
    /// Identifies a public text content block.
    /// </summary>
    public const string TEXT_CONTENT_TYPE = "text";

    /// <summary>
    /// Identifies a public structured content block.
    /// </summary>
    public const string JSON_CONTENT_TYPE = "json";

    /// <summary>
    /// Identifies the reviewed library resolver role.
    /// </summary>
    public const string CONTEXT7_RESOLVER_ROLE = "context7-resolve";

    /// <summary>
    /// Identifies the reviewed documentation query role.
    /// </summary>
    public const string CONTEXT7_QUERY_ROLE = "context7-query";

    /// <summary>
    /// Names the reviewed Context7 library resolver.
    /// </summary>
    public const string CONTEXT7_RESOLVER_TOOL = "resolve-library-id";

    /// <summary>
    /// Names the reviewed Context7 documentation operation.
    /// </summary>
    public const string CONTEXT7_QUERY_TOOL = "query-docs";

    /// <summary>
    /// Identifies the GitHub App configuration section.
    /// </summary>
    public const string GITHUB_SECTION = "GitHub";

    /// <summary>
    /// Identifies reviewed external-tool configuration.
    /// </summary>
    public const string EXTERNAL_SECTION = "ExternalMcp";

    /// <summary>
    /// Names the safe hosted MCP HttpClient.
    /// </summary>
    public const string EXTERNAL_HTTP_CLIENT = "ContextHub.ExternalMcp";

    /// <summary>
    /// Defines the fixed GitHub API authority.
    /// </summary>
    public const string GITHUB_API = "https://api.github.com/";

    /// <summary>
    /// Defines the GitHub request user-agent product.
    /// </summary>
    public const string USER_AGENT = "ContextHub/1.0";

    /// <summary>
    /// Identifies the supported protocol revision.
    /// </summary>
    public const string MCP_PROTOCOL = "2026-07-28";

    /// <summary>
    /// Identifies this service during native upstream MCP initialization.
    /// </summary>
    public const string MCP_CLIENT_NAME = "Context Hub";

    /// <summary>
    /// Identifies the existing upstream MCP client implementation version.
    /// </summary>
    public const string MCP_CLIENT_VERSION = "1.0.0";

    /// <summary>
    /// Defines the native transport timeout including buffered content.
    /// </summary>
    public const int REQUEST_TIMEOUT_SECONDS = 30;

    /// <summary>
    /// Defines the registry refresh period.
    /// </summary>
    public const int REFRESH_MINUTES = 10;

    /// <summary>
    /// Defines the sanitized persisted source failure summary.
    /// </summary>
    public const string SOURCE_SYNC_ERROR = "The source could not be inspected.";

    /// <summary>
    /// Identifies a confirmed transient PostgreSQL failure without publishing native provider data.
    /// </summary>
    public const string DATABASE_UNAVAILABLE = "integration_database_unavailable";

    /// <summary>
    /// Identifies an unavailable external endpoint resolver.
    /// </summary>
    public const string ENDPOINT_UNAVAILABLE = "external_endpoint_unavailable";

    /// <summary>
    /// Defines how long discovered definitions and routing references remain rebuildable.
    /// </summary>
    public const int SNAPSHOT_LIFETIME_MINUTES = 30;

    /// <summary>
    /// Bounds process-local copies of rebuildable hosted metadata.
    /// </summary>
    public const int LOCAL_CACHE_SECONDS = 15;

    /// <summary>
    /// Bounds the cached documentation result lifetime.
    /// </summary>
    public const int DOCUMENTATION_CACHE_MINUTES = 15;

    /// <summary>
    /// Bounds native and public hosted-tool response payloads.
    /// </summary>
    public const int MAX_RESPONSE_BYTES = 1_048_576;

    /// <summary>
    /// Bounds the number of accepted hosted content blocks.
    /// </summary>
    public const int MAX_CONTENT_BLOCKS = 32;

    /// <summary>
    /// Bounds each untrusted text block before projection.
    /// </summary>
    public const int MAX_TEXT_CHARACTERS = 32_768;

    /// <summary>
    /// Bounds the advertised input schema before sanitizing.
    /// </summary>
    public const int MAX_SCHEMA_CHARACTERS = 32_768;

    /// <summary>
    /// Rejects silently truncated upstream discovery.
    /// </summary>
    public const int MAX_DISCOVERED_TOOLS = 256;

    /// <summary>
    /// Identifies an opaque V1 managed-tool routing reference.
    /// </summary>
    public const string TOOL_REFERENCE_PREFIX = "mt_v1_";

    /// <summary>
    /// Identifies a usable discovered metadata summary.
    /// </summary>
    public const string DISCOVERY_READY_STATUS = "ready";

    /// <summary>
    /// Identifies a failed explicit source inspection.
    /// </summary>
    public const string SYNC_FAILED_STATUS = "failed";

    /// <summary>
    /// Identifies a completed explicit source inspection.
    /// </summary>
    public const string SYNC_SUCCESS_STATUS = "success";

    /// <summary>
    /// Names the database uniqueness constraint classified at the connection boundary.
    /// </summary>
    public const string EXTERNAL_NAME_CONSTRAINT = "UQ_Integration_ExternalMcp_Name";

    /// <summary>
    /// Keeps repository model metadata and duplicate-source classification aligned.
    /// </summary>
    public const string REPOSITORY_URL_CONSTRAINT = "UQ_Integration_Repository_Url";
}
