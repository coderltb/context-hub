namespace ContextHub.Api.Middlewares;

/// <summary>
/// Applies the MCP application budget after native authentication and endpoint authorization have succeeded.
/// </summary>
/// <param name="next">The next HTTP middleware.</param>
/// <param name="rateLimiter">The native client-identity limiter owned by DI.</param>
public sealed class ClientRateLimitMiddleware(
    RequestDelegate next,
    [FromKeyedServices(HubConstants.CLIENT_LIMITER)] PartitionedRateLimiter<string> rateLimiter)
{
    /// <summary>
    /// Uses only the introspected application identifier for the shared credential budget.
    /// </summary>
    /// <param name="context">The authorized HTTP request.</param>
    /// <param name="authService">The authenticated principal accessor.</param>
    /// <returns>The completed dispatch or a throttled response.</returns>
    public async Task InvokeAsync(
        HttpContext context,
        IAuthService authService)
    {
        // Owner APIs and health probes do not share an application's MCP allowance.
        if (!context.Request.Path.StartsWithSegments(HubConstants.MCP_PATH, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Authorization has already established this identity; every token for the client shares its budget.
        var clientId = authService.Get<Guid>(McpApplicationConstants.CLIENT_ID);
        using var lease = await rateLimiter.AcquireAsync(clientId.ToString(), cancellationToken: context.RequestAborted);

        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = HubConstants.RATE_LIMIT_WINDOW_SECONDS.ToString(CultureInfo.InvariantCulture);
            return;
        }

        await next(context);
    }
}
