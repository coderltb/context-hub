namespace ContextHub.ApiCommon.Handlers.Http;

/// <summary>
/// Propagates accepted correlation identifiers for explicitly opted-in HTTP clients.
/// </summary>
/// <param name="contextAccessor">The current HTTP request accessor, when a request exists.</param>
/// <param name="logger">The logger that excludes raw headers and destination URLs.</param>
public sealed class CorrelationIdHandler(
    IHttpContextAccessor contextAccessor,
    ILogger<CorrelationIdHandler> logger) : DelegatingHandler
{
    /// <summary>
    /// Preserves a valid caller header or propagates validated context and activity metadata.
    /// </summary>
    /// <param name="request">The outbound request.</param>
    /// <param name="cancellationToken">Cancellation from the HTTP caller.</param>
    /// <returns>The downstream transport response.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Headers.TryGetValues(ApiCommonConstants.CORRELATION_ID, out var values))
        {
            // Caller-owned identifiers are respected only after the same validation as inbound requests.
            var supplied = values.ToArray();
            if (supplied.Length == 1 && supplied[0].IsCanonicalCorrelationId())
            {
                return base.SendAsync(request, cancellationToken);
            }

            request.Headers.Remove(ApiCommonConstants.CORRELATION_ID);
            logger.LogWarning(ApiCommonLogConstants.CorrelationLogs.OUTBOUND_HEADER_REMOVED);
        }

        var contextual = contextAccessor.HttpContext?.Items[ApiCommonConstants.CORRELATION_ID] as string;
        var activityId = Activity.Current?.GetBaggageItem(ApiCommonConstants.CORRELATION_ID);
        var correlationId = contextual.IsCanonicalCorrelationId() ? contextual : activityId;
        if (correlationId.IsCanonicalCorrelationId())
        {
            request.Headers.Add(ApiCommonConstants.CORRELATION_ID, correlationId);
            logger.LogDebug(ApiCommonLogConstants.CorrelationLogs.OUTBOUND_PROPAGATED);
        }

        // Do not invent an unrelated request identifier when executing outside a correlated request.
        // Cancellation and transport exceptions retain their native semantics.
        return base.SendAsync(request, cancellationToken);
    }
}
