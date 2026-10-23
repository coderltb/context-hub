namespace ContextHub.Core.Interfaces.Http;

/// <summary>
/// Provides DTO-based verbs for named clients; typed JSON or text interpretation belongs to ThirdPartyApiService.
/// </summary>
public interface IRestClientMultipleService
{
    /// <summary>
    /// Returns the real GET response without buffering its body.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    Task<HttpResponseMessage> GetAsync(BaseHttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends POST with DTO content whose upload resources transport owns for this send.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    Task<HttpResponseMessage> PostAsync(BaseHttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends PUT with request-local credentials and DTO content.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    Task<HttpResponseMessage> PutAsync(BaseHttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends PATCH through the configured handler pipeline.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    Task<HttpResponseMessage> PatchAsync(BaseHttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the raw DELETE response for the calling integration to interpret.
    /// </summary>
    /// <param name="request">The DTO containing this call's client, endpoint, headers, query and body.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    Task<HttpResponseMessage> DeleteAsync(BaseHttpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a caller-owned native request through a named client; the caller owns its unread response.
    /// </summary>
    /// <param name="clientName">The registered named HTTP client selected for this call.</param>
    /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
    Task<HttpResponseMessage> SendAsync(
        string clientName,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
