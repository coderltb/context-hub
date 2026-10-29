namespace ContextHub.Shared.Dtos.Requests;

/// <summary>
/// Describes an integration request that feature-specific DTOs can extend without depending on ASP.NET.
/// </summary>
[PublicAPI]
public class BaseThirdPartyApiRequest
{
    /// <summary>
    /// Gets or sets the internally configured named HTTP client.
    /// </summary>
    public string HttpClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional request-specific base URL without changing the pooled client.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the absolute or relative provider endpoint.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP verb selected by the integration.
    /// </summary>
    public string Method { get; set; } = HttpMethod.Get.Method;

    /// <summary>
    /// Gets or sets serialized JSON or text; the injected serializer produces JSON when required.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets a stream whose ownership transfers to transport for this one send.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [JsonIgnore]
    public Stream? ContentStream { get; set; }

    /// <summary>
    /// Gets or sets URL-encoded or multipart fields, including repeated field names.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [JsonIgnore]
    public IEnumerable<KeyValuePair<string, string>>? FormData { get; set; }

    /// <summary>
    /// Gets or sets credentials for this request only; shared headers are never mutated.
    /// </summary>
    public AuthenticationHeaderValue? AuthenticationValue { get; set; }

    /// <summary>
    /// Gets or sets the media type; multipart transport supplies its own boundary.
    /// </summary>
    public string ContentType { get; set; } = SharedConstants.HttpVariables.JSON;

    /// <summary>
    /// Gets or sets request-local headers supplied by the integration.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets escaped query fields appended without modifying the endpoint property.
    /// </summary>
    public IReadOnlyDictionary<string, string>? QueryParameters { get; set; }

    /// <summary>
    /// Gets or sets upload content owned and disposed by multipart transport after sending.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [JsonIgnore]
    public HttpContent? File { get; set; }

    /// <summary>
    /// Gets or sets the multipart filename validated by native HTTP content.
    /// </summary>
    public string FileName { get; set; } = SharedConstants.HttpVariables.FILE_NAME;

    /// <summary>
    /// Gets or sets the provider's multipart file field name.
    /// </summary>
    public string FileFieldName { get; set; } = SharedConstants.HttpVariables.FILE_FIELD;
}
