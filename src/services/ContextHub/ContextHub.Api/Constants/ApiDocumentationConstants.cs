namespace ContextHub.Api.Constants;

/// <summary>
/// Defines this host's public document and security scheme identifiers.
/// </summary>
public static class ApiDocumentationConstants
{
    /// <summary>
    /// Identifies the API version shared by response metadata and the generated document.
    /// </summary>
    public const string API_VERSION = "1.0";

    /// <summary>
    /// Identifies the native versioned document registration.
    /// </summary>
    public const string DOCUMENT_NAME = "v1";

    /// <summary>
    /// Identifies the Development documentation route prefix.
    /// </summary>
    public const string SWAGGER_PREFIX = "swagger";

    /// <summary>
    /// Identifies the document loaded by Swagger UI.
    /// </summary>
    public const string DOCUMENT_PATH = "/openapi/" + DOCUMENT_NAME + ".json";

    /// <summary>
    /// Identifies the public PKCE client provisioned for this service.
    /// </summary>
    public const string SWAGGER_CLIENT_ID = "context-hub-swagger";

    /// <summary>
    /// Identifies the service in the native OpenAPI document.
    /// </summary>
    public const string DOCUMENT_TITLE = "Context Hub API";

    /// <summary>
    /// Identifies manually supplied access-token authorization.
    /// </summary>
    public const string BEARER_SCHEME = "Bearer";

    /// <summary>
    /// Identifies the standard HTTP authorization scheme.
    /// </summary>
    public const string BEARER_HTTP_SCHEME = "bearer";

    /// <summary>
    /// Identifies Development Swagger's authorization-code flow.
    /// </summary>
    public const string OAUTH_SCHEME = "OAuth2";

    /// <summary>
    /// Describes the single human administration scope.
    /// </summary>
    public const string ADMIN_SCOPE_DESCRIPTION = "Context Hub administration.";
}
