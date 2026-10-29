namespace ContextHub.Authentication.Api.Constants;

/// <summary>
/// Defines service-owned HTTP values separately from route templates and business classifications.
/// </summary>
public static class ApiValueConstants
{
    /// <summary>
    /// Applies native no-store before automatic model-binding rejection can short-circuit credential actions.
    /// </summary>
    public const int SENSITIVE_RESPONSE_CACHE_ORDER = -2003;

    /// <summary>
    /// Identifies this host's response and documentation contract version.
    /// </summary>
    public const string API_VERSION = "1.0";

    /// <summary>
    /// Names the single native OpenAPI document.
    /// </summary>
    public const string OPENAPI_DOCUMENT = "v1";

    /// <summary>
    /// Identifies the native document consumed by Swagger UI.
    /// </summary>
    public const string OPENAPI_PATH = "/openapi/" + OPENAPI_DOCUMENT + ".json";

    /// <summary>
    /// Identifies the development-only documentation UI.
    /// </summary>
    public const string SWAGGER_ROUTE = AuthenticationConstants.SWAGGER_PATH;

    /// <summary>
    /// Describes the document in the development UI.
    /// </summary>
    public const string DOCUMENT_TITLE = "Authentication API v1";

    /// <summary>
    /// Names the explicit Bearer authorization input.
    /// </summary>
    public const string BEARER_SCHEME = "Bearer";

    /// <summary>
    /// Names the deployment's exact-origin policy.
    /// </summary>
    public const string CORS_POLICY = "AuthenticationOrigins";

    /// <summary>
    /// Defines the lowercase HTTP authentication scheme required by OpenAPI.
    /// </summary>
    public const string BEARER_HTTP_SCHEME = "bearer";

    /// <summary>
    /// Names the development authorization-code alternative to manual Bearer entry.
    /// </summary>
    public const string OAUTH_SCHEME = "OAuth2";

    /// <summary>
    /// Describes the single scope requested by the development documentation client.
    /// </summary>
    public const string ADMIN_SCOPE_DESCRIPTION = "Use Authentication administration APIs.";

    /// <summary>
    /// Limits forwarded headers to one trusted immediate proxy hop.
    /// </summary>
    public const int FORWARDED_LIMIT = 1;
}
