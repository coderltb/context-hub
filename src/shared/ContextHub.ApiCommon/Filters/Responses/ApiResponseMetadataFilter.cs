namespace ContextHub.ApiCommon.Filters.Responses;

/// <summary>
/// Adds metadata only to explicit API response envelopes, without generic reflection or wrapping.
/// </summary>
/// <param name="metadataFactory">The request metadata factory.</param>
/// <param name="boundary">The API route scope selected by this host.</param>
public sealed class ApiResponseMetadataFilter(ApiResponseMetadataFactory metadataFactory, ApiResponseBoundary boundary) : IAsyncResultFilter
{
    /// <summary>
    /// Completes metadata immediately before executing an API object result.
    /// </summary>
    /// <param name="context">The current result filter context.</param>
    /// <param name="next">The next result filter.</param>
    /// <returns>The asynchronous result execution.</returns>
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        // Only explicit envelopes receive metadata; file, stream and native protocol results stay untouched.
        if (boundary.Contains(context.HttpContext.Request.Path) &&
            context.Result is ObjectResult { Value: IApiResponse response })
        {
            response.Meta = metadataFactory.Create(context.HttpContext);
        }

        await next();
    }
}
