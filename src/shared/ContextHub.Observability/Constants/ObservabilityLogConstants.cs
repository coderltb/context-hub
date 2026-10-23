namespace ContextHub.Observability.Constants;

/// <summary>
/// Stores developer-owned message templates separate from configuration values and error messages.
/// </summary>
public static class ObservabilityLogConstants
{
    /// <summary>
    /// One completion record per HTTP request, without raw URL, query or body content.
    /// </summary>
    public const string REQUEST_COMPLETED = "HTTP {RequestMethod} completed with {StatusCode} in {Elapsed:0.0000} ms.";
}
