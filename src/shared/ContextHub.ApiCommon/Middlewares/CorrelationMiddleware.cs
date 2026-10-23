namespace ContextHub.ApiCommon.Middlewares;

/// <summary>
/// Owns correlation UUID validation, timestamps, trace enrichment and response headers.
/// </summary>
/// <param name="next">The next middleware.</param>
/// <param name="timeProvider">The host-owned clock.</param>
/// <param name="logger">The safe structured logger.</param>
public sealed class CorrelationMiddleware(
    RequestDelegate next,
    TimeProvider timeProvider,
    ILogger<CorrelationMiddleware> logger)
{
    /// <summary>
    /// Preserves one valid canonical UUID or replaces it without logging rejected raw input.
    /// </summary>
    /// <param name="context">The current HTTP request.</param>
    /// <returns>The asynchronous downstream request.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Accept exactly one canonical UUID. Multiple or malformed values must not enter logs or trace baggage.
        var values = context.Request.Headers[ApiCommonConstants.CORRELATION_ID];
        var incoming = values.Count == 1 ? values[0] : null;
        var preserved = incoming.IsCanonicalCorrelationId();
        var correlationId = preserved
            ? incoming!
            : Guid.NewGuid().ToString("D");
        var timestamp = timeProvider.GetUtcNow();

        // ASP.NET normally supplies a W3C request activity. Isolate legacy activities rather than emitting invalid trace IDs.
        using var ownedActivity = Activity.Current?.IdFormat == ActivityIdFormat.W3C
            ? null
            : new Activity(ApiCommonConstants.HTTP_ACTIVITY)
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
        var activity = Activity.Current!;

        // Preserve host-owned trace state so enrichment cannot leak beyond this middleware invocation.
        var previousTag = activity.GetTagItem(ApiCommonConstants.CORRELATION_ID);
        var previousBaggage = activity.GetBaggageItem(ApiCommonConstants.CORRELATION_ID);
        activity.SetTag(ApiCommonConstants.CORRELATION_ID, correlationId);
        activity.SetBaggage(ApiCommonConstants.CORRELATION_ID, correlationId);

        // Store one metadata authority for filters and error adapters; they must not regenerate identifiers.
        context.Items[ApiCommonConstants.CORRELATION_ID] = correlationId;
        context.Items[ApiCommonConstants.REQUEST_TIMESTAMP] = timestamp;
        context.Items[ApiCommonConstants.REQUEST_ACTIVITY] = activity;
        context.Response.Headers[ApiCommonConstants.CORRELATION_ID] = correlationId;

        // Restore the canonical header immediately before writing, even if downstream replaced it.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[ApiCommonConstants.CORRELATION_ID] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestId"] = context.TraceIdentifier,
            ["TraceId"] = activity.TraceId.ToString(),
            ["SpanId"] = activity.SpanId.ToString()
        });

        logger.LogDebug(
            ApiCommonLogConstants.CorrelationLogs.INITIALIZED,
            context.TraceIdentifier,
            correlationId,
            preserved);
        if (values.Count > 0 && !preserved)
        {
            logger.LogWarning(ApiCommonLogConstants.CorrelationLogs.HEADER_REPLACED, context.TraceIdentifier);
        }

        try
        {
            await next(context);
            logger.LogDebug(
                ApiCommonLogConstants.CorrelationLogs.DOWNSTREAM_RETURNED,
                context.TraceIdentifier,
                context.Response.StatusCode);
        }
        finally
        {
            // Cleanup also runs after exceptions and cancellation, without disposing the host's activity.
            activity.SetTag(ApiCommonConstants.CORRELATION_ID, previousTag);
            activity.SetBaggage(ApiCommonConstants.CORRELATION_ID, previousBaggage);
        }
    }
}
