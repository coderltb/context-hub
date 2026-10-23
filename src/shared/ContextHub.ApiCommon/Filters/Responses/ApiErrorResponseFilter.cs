namespace ContextHub.ApiCommon.Filters.Responses;

/// <summary>
/// Adapts native MVC status results inside the configured API boundary before MVC creates a problem response.
/// </summary>
/// <param name="boundary">The host-owned API route scope.</param>
/// <param name="errorFactory">The existing safe API error factory.</param>
/// <param name="logger">The logger for status-only adapter diagnostics.</param>
public sealed class ApiErrorResponseFilter(
    ApiResponseBoundary boundary,
    ApiErrorResponseFactory errorFactory,
    ILogger<ApiErrorResponseFilter> logger) : IAsyncAlwaysRunResultFilter, IOrderedFilter
{
    /// <summary>
    /// Gets the ordering that precedes MVC's built-in client-error mapping.
    /// </summary>
    public int Order => ApiCommonConstants.API_ERROR_FILTER_ORDER;

    /// <summary>
    /// Converts supported bodyless controller errors without rewriting native protocols or explicit result bodies.
    /// </summary>
    /// <param name="context">The current MVC result context.</param>
    /// <param name="next">The next result filter.</param>
    /// <returns>The asynchronous execution of the resulting response.</returns>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Always-run filters also see short-circuited MVC results, such as unsupported input media types.
        // Only native status results are adapted; files, streams and application-defined bodies remain intact.
        if (!context.HttpContext.Response.HasStarted && boundary.Contains(context.HttpContext.Request.Path) &&
            context.Result is StatusCodeResult { StatusCode: var status } && status.IsSupportedApiError())
        {
            context.Result = new ObjectResult(errorFactory.Create(context.HttpContext, status))
            {
                StatusCode = status,
                ContentTypes = { ApiCommonConstants.JSON_CONTENT_TYPE }
            };
            logger.LogDebug(ApiCommonLogConstants.ResponseLogs.NATIVE_ERROR_MAPPED, status);
        }

        await next();
    }
}
