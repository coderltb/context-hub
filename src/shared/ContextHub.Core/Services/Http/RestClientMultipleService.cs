namespace ContextHub.Core.Services.Http;

/// <summary>
/// Sends the reference request DTO through named clients, keeping credentials local to each native request.
/// </summary>
/// <param name="clientFactory">The DI-managed factory for internally configured clients and handlers.</param>
/// <param name="logger">The metadata-only transport logger.</param>
public sealed class RestClientMultipleService(
    IHttpClientFactory clientFactory,
    ILogger<RestClientMultipleService> logger) : IRestClientMultipleService
{
    /// <summary>
    /// Sends DTO GET without interpreting or buffering the response body.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    public Task<HttpResponseMessage> GetAsync(BaseHttpRequest request, CancellationToken cancellationToken = default) =>
        SendRequestAsync(request, HttpMethod.Get, cancellationToken);

    /// <summary>
    /// Sends DTO POST using its declared JSON, form, multipart or stream payload.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    public Task<HttpResponseMessage> PostAsync(BaseHttpRequest request, CancellationToken cancellationToken = default) =>
        SendRequestAsync(request, HttpMethod.Post, cancellationToken);

    /// <summary>
    /// Sends DTO PUT through the selected named client's handler pipeline.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    public Task<HttpResponseMessage> PutAsync(BaseHttpRequest request, CancellationToken cancellationToken = default) =>
        SendRequestAsync(request, HttpMethod.Put, cancellationToken);

    /// <summary>
    /// Sends DTO PATCH without replaying unsafe operations or modifying the DTO.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    public Task<HttpResponseMessage> PatchAsync(BaseHttpRequest request, CancellationToken cancellationToken = default) =>
        SendRequestAsync(request, HttpMethod.Patch, cancellationToken);

    /// <summary>
    /// Returns the actual DTO DELETE response for the integration to interpret.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    public Task<HttpResponseMessage> DeleteAsync(BaseHttpRequest request, CancellationToken cancellationToken = default) =>
        SendRequestAsync(request, HttpMethod.Delete, cancellationToken);

    /// <summary>
    /// Sends a caller-owned native request and returns an unread caller-owned response.
    /// </summary>
    /// <param name="clientName">The registered named HTTP client selected for this call.</param>
    /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    public async Task<HttpResponseMessage> SendAsync(
        string clientName,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        ArgumentNullException.ThrowIfNull(request);

        logger.LogDebug(CoreLogConstants.HttpLogs.CLIENT_SELECTED, clientName);
        logger.LogDebug(CoreLogConstants.HttpLogs.SEND_STARTED, request.Method.Method);

        try
        {
            // A factory client owns neither the pooled handler nor the unread response returned to the integration.
            using var client = clientFactory.CreateClient(clientName);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            logger.LogDebug(CoreLogConstants.HttpLogs.SEND_COMPLETED, request.Method.Method, (int)response.StatusCode);

            return response;
        }
        catch (OperationCanceledException)
        {
            // Preserve timeout/cancellation semantics rather than synthesizing a gateway response or retrying unsafe work.
            logger.LogDebug(CoreLogConstants.HttpLogs.SEND_CANCELLED, clientName, request.Method.Method);
            throw;
        }
        catch (HttpRequestException exception)
        {
            // Provider exception messages may include credentials. Log the failure type, then retain the original exception.
            logger.LogWarning(CoreLogConstants.HttpLogs.SEND_FAILED, clientName, request.Method.Method, exception.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Constructs and disposes one native DTO request without changing shared client headers or base address.
    /// </summary>
    /// <param name="dto">The transport DTO containing endpoint, headers, query and payload settings.</param>
    /// <param name="method">The HTTP verb used to dispatch this request.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    private async Task<HttpResponseMessage> SendRequestAsync(BaseHttpRequest dto, HttpMethod method, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.HttpClientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.RequestUri);

        // Resolve the endpoint locally so a shared named client and the caller's DTO keep their original settings.
        var uri = dto.RequestUri;

        if (!string.IsNullOrWhiteSpace(dto.BaseAddress))
        {
            var baseAddress = dto.BaseAddress.EndsWith('/') ? dto.BaseAddress : dto.BaseAddress + "/";
            uri = new Uri(new Uri(baseAddress, UriKind.Absolute), uri).AbsoluteUri;
        }

        using var request = new HttpRequestMessage(method, AppendQuery(uri, dto.AdditionalQueryParams));

        if (dto.AdditionalHeaders is not null)
        {
            foreach (var (name, value) in dto.AdditionalHeaders)
            {
                request.Headers.Add(name, value);
            }
        }

        // Credentials belong to this request, even when the same named configuration serves concurrent identities.
        if (dto.AuthenticationValue is not null)
        {
            request.Headers.Authorization = dto.AuthenticationValue;
        }

        if (method != HttpMethod.Get && method != HttpMethod.Delete)
        {
            request.Content = CreateContent(dto);
        }

        // Native request disposal releases DTO uploads; callers provide a fresh upload for each send.
        return await SendAsync(dto.HttpClientName, request, cancellationToken);
    }

    /// <summary>
    /// Creates native content according to the request media type, including repeated form fields and uploads.
    /// </summary>
    /// <param name="dto">The transport DTO containing endpoint, headers, query and payload settings.</param>
    /// <returns>Native HTTP content whose ownership transfers to the DTO request.</returns>
    private static HttpContent CreateContent(BaseHttpRequest dto)
    {
        // Parameters and casing do not select the payload kind; native content owns its multipart boundary.
        MediaTypeHeaderValue.TryParse(dto.ContentType, out var contentType);
        if (string.Equals(contentType?.MediaType, SharedConstants.HttpVariables.FORM, StringComparison.OrdinalIgnoreCase))
        {
            return new FormUrlEncodedContent(dto.RequestFormData ?? []);
        }

        if (string.Equals(contentType?.MediaType, SharedConstants.HttpVariables.MULTIPART, StringComparison.OrdinalIgnoreCase))
        {
            var multipart = new MultipartFormDataContent();

            try
            {
                foreach (var field in dto.RequestFormData ?? [])
                {
                    multipart.Add(new StringContent(field.Value), field.Key);
                }

                if (dto.File is not null)
                {
                    multipart.Add(dto.File, dto.FileFieldName, dto.FileName);
                }

                return multipart;
            }
            catch
            {
                // Failed construction has no request owner yet; release content already attached here.
                multipart.Dispose();
                throw;
            }
        }

        HttpContent content = dto.RequestStream is null
            ? new StringContent(dto.RequestData ?? string.Empty, Encoding.UTF8)
            : new StreamContent(dto.RequestStream);

        try
        {
            // An invalid header still fails natively, but only after content has taken ownership so the catch releases a DTO stream.
            content.Headers.ContentType = contentType ?? MediaTypeHeaderValue.Parse(dto.ContentType);

            return content;
        }
        catch
        {
            // No request owns this content yet; release it and let native disposal failures propagate.
            content.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Appends escaped query fields before a fragment without replacing existing parameters or mutating the DTO.
    /// </summary>
    /// <param name="uri">The endpoint before query fields are appended.</param>
    /// <param name="fields">Query fields whose names and values are URI-escaped without mutating the input.</param>
    /// <returns>The endpoint with escaped query fields before its original fragment.</returns>
    private static string AppendQuery(string uri, IReadOnlyDictionary<string, string>? fields)
    {
        if (fields is not { Count: > 0 })
        {
            return uri;
        }

        var fragmentIndex = uri.IndexOf('#');
        var fragment = fragmentIndex < 0 ? string.Empty : uri[fragmentIndex..];
        var address = fragmentIndex < 0 ? uri : uri[..fragmentIndex];
        var separator = address.EndsWith('?') || address.EndsWith('&') ? string.Empty : address.Contains('?') ? "&" : "?";
        var query = string.Join("&", fields.Select(field => $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(field.Value)}"));

        return address + separator + query + fragment;
    }
}
