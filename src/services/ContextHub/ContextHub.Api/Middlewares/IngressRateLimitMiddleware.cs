namespace ContextHub.Api.Middlewares;

/// <summary>
/// Bounds protected-route ingress before a request can consume an Authentication backchannel call.
/// </summary>
/// <param name="next">The next HTTP middleware.</param>
/// <param name="rateLimiter">The native per-address limiter owned by DI.</param>
public sealed class IngressRateLimitMiddleware(
    RequestDelegate next,
    [FromKeyedServices(HubConstants.INGRESS_LIMITER)] PartitionedRateLimiter<string> rateLimiter)
{
    /// <summary>
    /// Applies the ingress budget without depending on token or browser metadata.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <returns>The completed dispatch or a throttled response.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Anonymous probes and unrelated paths must not consume the protected ingress allowance.
        if (!context.Request.Path.StartsWithSegments(HubConstants.MCP_PATH, StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Path.StartsWithSegments(HubConstants.ADMIN_PATH, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Charge the network address before introspection; caller-supplied identity cannot create another partition.
        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? HubConstants.UNKNOWN_REMOTE_ADDRESS;
        using var lease = await rateLimiter.AcquireAsync(remoteAddress, cancellationToken: context.RequestAborted);

        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = HubConstants.RATE_LIMIT_WINDOW_SECONDS.ToString(CultureInfo.InvariantCulture);
            return;
        }

        await next(context);
    }
}
