namespace ContextHub.ApiCommon.Factories.OpenApi;

/// <summary>
/// Produces safe, deterministic envelope fixtures using the same error mapping as runtime adapters.
/// </summary>
public static class ApiErrorExampleFactory
{
    /// <summary>
    /// Creates an error fixture without reading a real request, clock, credential or database.
    /// </summary>
    /// <param name="status">The approved error status.</param>
    /// <param name="apiVersion">The same public version selected for runtime responses.</param>
    /// <returns>The standard wire envelope with fixed synthetic trace metadata.</returns>
    public static ApiResponse<object> Create(
        int status,
        string apiVersion)
    {
        // Reuse the runtime mapper so changing a safe error message updates runtime and documentation together.
        var (code, message) = status.ToApiError();

        // Fixed synthetic metadata keeps generated documents deterministic and prevents leaking a real request identity.
        return new(error: new ApiError(code, message, []))
        {
            Meta = CreateMetadata(apiVersion)
        };
    }

    /// <summary>
    /// Creates shared synthetic HTTP metadata for success and error documentation.
    /// </summary>
    /// <param name="apiVersion">The host-owned public API version.</param>
    /// <returns>Fixed documentation metadata with no runtime request identity.</returns>
    public static ApiResponseMeta CreateMetadata(string apiVersion) =>
        new(
            ApiCommonOpenApiConstants.EXAMPLE_REQUEST_ID,
            ApiCommonOpenApiConstants.EXAMPLE_CORRELATION_ID,
            ApiCommonOpenApiConstants.EXAMPLE_TRACE_ID,
            ApiCommonOpenApiConstants.EXAMPLE_SPAN_ID,
            apiVersion,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
}
