namespace ContextHub.ApiCommon.Exceptions;

/// <summary>
/// Declares a supported HTTP failure while keeping optional diagnostic context outside the global response contract.
/// </summary>
public class ApiException : Exception
{
    /// <summary>
    /// Selects a supported status and reuses its standard error defaults without a second mapping table.
    /// </summary>
    /// <param name="statusCode">The HTTP failure supported by the API error adapter.</param>
    /// <param name="errorCode">Optional internal diagnostic context; blank uses the standard code.</param>
    /// <param name="publicMessage">Optional declared description; the global handler never publishes this override.</param>
    public ApiException(int statusCode, string? errorCode = null, string? publicMessage = null)
        : base(publicMessage)
    {
        // The existing mapper rejects unsupported statuses once, before any response can be written.
        var standard = statusCode.ToApiError();

        StatusCode = statusCode;
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? standard.Code : errorCode;
        PublicMessage = string.IsNullOrWhiteSpace(publicMessage) ? standard.Message : publicMessage;
    }

    /// <summary>
    /// Gets the explicitly supported HTTP status selected by API code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the diagnostic identifier; global responses always use the standard status mapping instead.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the declared diagnostic description, which does not override the global security boundary.
    /// </summary>
    public string PublicMessage { get; }
}
