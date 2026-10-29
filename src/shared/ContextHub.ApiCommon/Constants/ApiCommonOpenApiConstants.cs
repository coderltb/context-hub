namespace ContextHub.ApiCommon.Constants;

/// <summary>
/// Defines stable documentation identifiers separately from log templates and error messages.
/// </summary>
public static class ApiCommonOpenApiConstants
{
    /// <summary>
    /// Names the default native OpenAPI document.
    /// </summary>
    public const string DEFAULT_DOCUMENT_NAME = "v1";

    /// <summary>
    /// Names a response example when the endpoint declares only one variant.
    /// </summary>
    public const string DEFAULT_EXAMPLE_NAME = "default";

    /// <summary>
    /// Identifies vendor media types carrying JSON payloads.
    /// </summary>
    public const string JSON_MEDIA_SUFFIX = "+json";

    /// <summary>
    /// Identifies synthetic documentation metadata rather than a live request.
    /// </summary>
    public const string EXAMPLE_REQUEST_ID = "documentation-example";

    /// <summary>
    /// Supplies a fixed synthetic correlation UUID.
    /// </summary>
    public const string EXAMPLE_CORRELATION_ID = "00000000-0000-4000-8000-000000000001";

    /// <summary>
    /// Supplies a synthetic W3C trace identifier.
    /// </summary>
    public const string EXAMPLE_TRACE_ID = "00000000000000000000000000000001";

    /// <summary>
    /// Supplies a synthetic W3C span identifier.
    /// </summary>
    public const string EXAMPLE_SPAN_ID = "0000000000000001";

    /// <summary>
    /// Identifies the local Swagger operation-search asset.
    /// </summary>
    public const string SEARCH_SCRIPT_PATH = "/swagger-assets/operation-search.js";

    /// <summary>
    /// Identifies the script embedded in the shared API assembly.
    /// </summary>
    public const string SEARCH_SCRIPT_RESOURCE = "ContextHub.ApiCommon.Assets.Swagger.operation-search.js";

    /// <summary>
    /// Declares the browser script media type.
    /// </summary>
    public const string JAVASCRIPT_CONTENT_TYPE = "application/javascript; charset=utf-8";

    /// <summary>
    /// Reports a packaging error without including request data.
    /// </summary>
    public const string SEARCH_SCRIPT_MISSING = "The embedded Swagger operation-search script is missing.";

    /// <summary>
    /// Names the configuration section bound to native Swagger UI options.
    /// </summary>
    public const string SWAGGER_UI_SECTION = "SwaggerUI";
}
