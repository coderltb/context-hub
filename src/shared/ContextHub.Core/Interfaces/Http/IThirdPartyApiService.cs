namespace ContextHub.Core.Interfaces.Http;

/// <summary>
/// Provides the reference integration flow: dispatch a request, mask credential fields, then read typed JSON or text.
/// </summary>
public interface IThirdPartyApiService
{
    /// <summary>
    /// Returns successful provider text with credential values masked and disposes the consumed response.
    /// </summary>
    /// <typeparam name="TRequest">The integration request type extending the shared request contract.</typeparam>
    /// <param name="request">Integration fields and an internally registered client name; credentials stay request-local.</param>
    /// <param name="cancellationToken">Cancels sending and response consumption without manufacturing an HTTP response.</param>
    /// <returns>Credential-masked text within the shared convenience-read byte limit; ordinary fields remain visible.</returns>
    /// <exception cref="HttpRequestException">The provider returned a failure status; the exception retains that status, not its body.</exception>
    Task<string> HandleApiData<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : BaseThirdPartyApiRequest;

    /// <summary>
    /// Deserializes credential-masked successful provider JSON through the injected Newtonsoft serializer.
    /// </summary>
    /// <typeparam name="TRequest">The integration request type extending the shared request contract.</typeparam>
    /// <typeparam name="TResponse">The provider response type deserialized from credential-masked JSON.</typeparam>
    /// <param name="request">The extensible integration DTO; it is mapped, never serialized as the request body.</param>
    /// <param name="cancellationToken">Cancels sending and bounded response consumption.</param>
    /// <returns>The typed value, or default for blank or JSON null content; malformed JSON raises a native parse error.</returns>
    Task<TResponse?> HandleApiData<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : BaseThirdPartyApiRequest;

    /// <summary>
    /// Dispatches the selected verb and leaves raw response reading and disposal to the caller.
    /// </summary>
    /// <param name="request">The provider request; DTO upload resources transfer to transport for this send.</param>
    /// <param name="cancellationToken">Cancellation passed through the selected client's handler pipeline.</param>
    /// <returns>An unread caller-owned response, including non-success statuses.</returns>
    Task<HttpResponseMessage> HandleDynamicHttpRequest(
        BaseThirdPartyApiRequest request,
        CancellationToken cancellationToken = default);
}
