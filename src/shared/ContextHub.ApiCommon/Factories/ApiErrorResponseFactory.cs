namespace ContextHub.ApiCommon.Factories;

/// <summary>
/// Creates approved English API errors while preserving their HTTP status outside the JSON body.
/// </summary>
/// <param name="metadataFactory">The request metadata factory.</param>
public sealed class ApiErrorResponseFactory(ApiResponseMetadataFactory metadataFactory)
{
    /// <summary>
    /// Builds an error envelope without leaking an exception message.
    /// </summary>
    /// <param name="context">The request with initialized metadata.</param>
    /// <param name="statusCode">An approved HTTP error status.</param>
    /// <param name="details">Validated safe field issues, if applicable.</param>
    /// <returns>The public error envelope.</returns>
    public ApiResponse<object> Create(
        HttpContext context,
        int statusCode,
        IReadOnlyList<ApiErrorDetail>? details = null)
    {
        var (code, message) = statusCode.ToApiError();
        return new ApiResponse<object>(error: new ApiError(code, message, details ?? []))
        {
            Meta = metadataFactory.Create(context)
        };
    }

    /// <summary>
    /// Writes a JSON error without clearing protocol headers established upstream.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="statusCode">The HTTP status to retain.</param>
    /// <param name="cancellationToken">Stops the response write when the caller disconnects.</param>
    /// <returns>The response write operation.</returns>
    public Task WriteAsync(
        HttpContext context,
        int statusCode,
        CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = statusCode;
        // Empty-status middleware may have set a zero length. Clear it before emitting the replacement JSON body.
        context.Response.ContentLength = null;
        return context.Response.WriteAsJsonAsync(Create(context, statusCode), cancellationToken);
    }
}
