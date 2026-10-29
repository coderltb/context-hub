namespace ContextHub.Shared.Dtos.Responses;

/// <summary>
/// Represents an explicit API JSON response, not an MCP, OIDC or stream response.
/// </summary>
/// <typeparam name="T">The feature's reference-type result DTO.</typeparam>
public sealed class ApiResponse<T> : IApiResponse where T : class
{
    /// <summary>
    /// Creates a response and guarantees that errors never retain success data.
    /// </summary>
    /// <param name="data">The successful result, or null.</param>
    /// <param name="error">The safe transport error, or null.</param>
    public ApiResponse(T? data = null, ApiError? error = null)
    {
        // Derive the data branch from error presence so consumers never receive success data on a failure.
        Data = error is null ? data : null;
        Error = error;
    }

    /// <summary>
    /// Gets success from the error state; callers cannot set conflicting values.
    /// </summary>
    /// <example>true</example>
    [JsonPropertyName("success")]
    public bool Success => Error is null;

    /// <summary>
    /// Gets successful data, always null on failure.
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; }

    /// <summary>
    /// Gets a safe error, or null on success.
    /// </summary>
    /// <example>null</example>
    [JsonPropertyName("error")]
    public ApiError? Error { get; }

    /// <summary>
    /// Gets or sets metadata supplied by the HTTP adapter.
    /// </summary>
    [JsonPropertyName("meta")]
    public ApiResponseMeta? Meta { get; set; }
}
