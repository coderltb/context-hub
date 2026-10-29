namespace ContextHub.Shared.Dtos.Requests;

/// <summary>
/// Groups named-client REST request data without workspace, framework or credential-storage behavior.
/// </summary>
[PublicAPI]
public sealed class BaseHttpRequest
{
    /// <summary>
    /// Gets or sets the internally registered client name required by the multiple-client service.
    /// </summary>
    public string HttpClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute or relative URI; transport never rewrites this property.
    /// </summary>
    public string RequestUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets already serialized JSON/XML/text; use the injected serializer when constructing JSON data.
    /// </summary>
    public string? RequestData { get; set; }

    /// <summary>
    /// Gets or sets a raw stream owned and disposed by transport for this one send.
    /// </summary>
    public Stream? RequestStream { get; set; }

    /// <summary>
    /// Gets or sets materialized form fields for URL-encoded or multipart requests.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>>? RequestFormData { get; set; }

    /// <summary>
    /// Gets or sets authentication for this request only, never shared DefaultRequestHeaders.
    /// </summary>
    public AuthenticationHeaderValue? AuthenticationValue { get; set; }

    /// <summary>
    /// Gets or sets an optional per-request base address without mutating the configured HttpClient.
    /// </summary>
    public string? BaseAddress { get; set; }

    /// <summary>
    /// Gets or sets the payload media type; native multipart content supplies its own boundary.
    /// </summary>
    public string ContentType { get; set; } = SharedConstants.HttpVariables.JSON;

    /// <summary>
    /// Gets or sets per-request additional headers.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalHeaders { get; set; }

    /// <summary>
    /// Gets or sets extra query fields appended to existing query parameters with escaping.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalQueryParams { get; set; }

    /// <summary>
    /// Gets or sets upload content owned by transport instead of an ASP.NET IFormFile dependency.
    /// </summary>
    public HttpContent? File { get; set; }

    /// <summary>
    /// Gets or sets the multipart filename passed through native content-disposition validation.
    /// </summary>
    public string FileName { get; set; } = SharedConstants.HttpVariables.FILE_NAME;

    /// <summary>
    /// Gets or sets the integration's multipart file field name.
    /// </summary>
    public string FileFieldName { get; set; } = SharedConstants.HttpVariables.FILE_FIELD;
}
