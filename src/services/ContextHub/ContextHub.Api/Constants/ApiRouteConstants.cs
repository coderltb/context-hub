namespace ContextHub.Api.Constants;

/// <summary>
/// Defines the process-only endpoint without introducing catalog or authentication behavior into this host.
/// </summary>
public static class ApiRouteConstants
{
    /// <summary>
    /// Identifies the anonymous native liveness endpoint.
    /// </summary>
    public const string LIVENESS = "/health/live";
}
