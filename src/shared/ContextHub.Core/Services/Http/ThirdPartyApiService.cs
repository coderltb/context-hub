namespace ContextHub.Core.Services.Http;

/// <summary>
/// Keeps the reference third-party flow with DI-managed transport, startup mappings and Newtonsoft serialization.
/// </summary>
/// <param name="restClientService">The named-client transport owning each outgoing request.</param>
/// <param name="jsonSerializerService">The shared JSON contract used for typed responses.</param>
/// <param name="logger">The provider logger receiving credential-masked request and response text.</param>
public class ThirdPartyApiService(
    IRestClientMultipleService restClientService,
    IJsonSerializerService jsonSerializerService,
    ILogger<ThirdPartyApiService> logger) : IThirdPartyApiService
{
    /// <summary>
    /// Logs the masked request and returns successful response text with credential fields redacted.
    /// </summary>
    /// <typeparam name="TRequest">The feature request extending the shared provider contract.</typeparam>
    /// <param name="request">The integration DTO; private and upload properties are excluded by its JSON contract.</param>
    /// <param name="cancellationToken">Cancellation propagated through dispatch and bounded content consumption.</param>
    /// <returns>The successful provider payload with only identified credential values masked.</returns>
    public virtual async Task<string> HandleApiData<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : BaseThirdPartyApiRequest
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Follow the reference flow: serialize once, mask credentials and let the configured logger filter its level.
        var requestJson = jsonSerializerService.Serialize(request);
        var maskedRequest = LogMaskingHelper.MaskAllSensitiveData(requestJson);

        logger.LogInformation(CoreLogConstants.HttpLogs.PROVIDER_STARTED, typeof(TRequest).Name, maskedRequest);

        // Dispatch through the configured named client and its handlers before interpreting provider-specific content.
        using var response = await HandleDynamicHttpRequest(request, cancellationToken);

        // ResponseHeadersRead ends the send stage at headers. Bound body consumption separately without replacing caller cancellation.
        using var bodyCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bodyCancellation.CancelAfter(TimeSpan.FromSeconds(CoreConstants.HttpVariables.BODY_READ_TIMEOUT_SECONDS));

        // Keep native read failures with the caller; the response and linked deadline remain locally owned.
        await response.Content.LoadIntoBufferAsync(CoreConstants.HttpVariables.MAX_RESPONSE_BYTES, bodyCancellation.Token);
        var content = await response.Content.ReadAsStringAsync(bodyCancellation.Token);

        var maskedContent = LogMaskingHelper.MaskAllSensitiveData(content);

        // Preserve the upstream status but never include provider credentials or body text in the public exception.
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                CoreLogConstants.HttpLogs.PROVIDER_FAILED,
                typeof(TRequest).Name,
                (int)response.StatusCode,
                maskedContent);

            throw new HttpRequestException(CoreErrorConstants.HttpErrors.PROVIDER_FAILED, null, response.StatusCode);
        }

        // Non-secret response fields remain available; masking applies equally to raw text and the typed overload below.
        logger.LogInformation(
            CoreLogConstants.HttpLogs.PROVIDER_COMPLETED,
            typeof(TRequest).Name,
            (int)response.StatusCode,
            maskedContent);

        return maskedContent;
    }

    /// <summary>
    /// Uses the same status and read boundary before deserializing with the injected Newtonsoft contract.
    /// </summary>
    /// <typeparam name="TRequest">The feature request extending the shared provider contract.</typeparam>
    /// <typeparam name="TResponse">The provider-specific JSON response type.</typeparam>
    /// <param name="request">The configured integration request.</param>
    /// <param name="cancellationToken">Cancellation for dispatch and response consumption.</param>
    /// <returns>The credential-masked response, or null when the successful JSON payload represents no value.</returns>
    public virtual async Task<TResponse?> HandleApiData<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : BaseThirdPartyApiRequest
    {
        var content = await HandleApiData(request, cancellationToken);

        return jsonSerializerService.Deserialize<TResponse>(content);
    }

    /// <summary>
    /// Maps neutral request fields and dispatches the selected verb without replacing native transport exceptions.
    /// </summary>
    /// <param name="request">The transport DTO, including query fields and per-request headers.</param>
    /// <param name="cancellationToken">Cancellation passed to the configured HTTP handler pipeline.</param>
    /// <returns>The unread upstream response that the caller owns and must dispose.</returns>
    public Task<HttpResponseMessage> HandleDynamicHttpRequest(
        BaseThirdPartyApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Reuse the startup transport profile; resource ownership stays explicit below.
        var dto = request.Adapt<BaseThirdPartyApiRequest, BaseHttpRequest>();
        dto.File = request.File;
        dto.RequestStream = request.ContentStream;

        // Normalize only the verb locally; do not rewrite the caller's DTO or serialize its extension properties.
        return request.Method.ToUpperInvariant() switch
        {
            "GET" => restClientService.GetAsync(dto, cancellationToken),
            "POST" => restClientService.PostAsync(dto, cancellationToken),
            "PUT" => restClientService.PutAsync(dto, cancellationToken),
            "PATCH" => restClientService.PatchAsync(dto, cancellationToken),
            "DELETE" => restClientService.DeleteAsync(dto, cancellationToken),
            _ => throw new ArgumentException(CoreErrorConstants.HttpErrors.METHOD_NOT_SUPPORTED, nameof(request))
        };
    }
}
