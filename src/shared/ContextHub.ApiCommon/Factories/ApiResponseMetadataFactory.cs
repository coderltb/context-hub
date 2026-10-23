namespace ContextHub.ApiCommon.Factories;

/// <summary>
/// Reads metadata established by correlation middleware without generating replacement identifiers.
/// </summary>
/// <param name="timeProvider">The host-owned time provider.</param>
/// <param name="boundary">The immutable response version selected by this host.</param>
public sealed class ApiResponseMetadataFactory(TimeProvider timeProvider, ApiResponseBoundary boundary)
{
    /// <summary>
    /// Creates metadata for the current request; a wrong middleware order fails explicitly.
    /// </summary>
    /// <param name="context">The current HTTP request.</param>
    /// <returns>Metadata with UTC timestamps and the active W3C trace.</returns>
    public ApiResponseMeta Create(HttpContext context)
    {
        // Missing metadata means pipeline ordering is wrong; generating replacements would hide that defect.
        if (context.Items[ApiCommonConstants.CORRELATION_ID] is not string correlationId ||
            context.Items[ApiCommonConstants.REQUEST_TIMESTAMP] is not DateTimeOffset requestTimestamp ||
            context.Items[ApiCommonConstants.REQUEST_ACTIVITY] is not Activity activity)
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.MetadataErrors.NOT_INITIALIZED);
        }

        return new ApiResponseMeta(
            context.TraceIdentifier,
            correlationId,
            activity.TraceId.ToString(),
            activity.SpanId.ToString(),
            boundary.ApiVersion,
            requestTimestamp,
            timeProvider.GetUtcNow());
    }
}
