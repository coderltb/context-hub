namespace ContextHub.Api.Constants;

/// <summary>
/// Defines anonymous native process and dependency probe routes.
/// </summary>
public static class ApiRouteConstants
{
    /// <summary>
    /// Groups anonymous process and dependency probes outside the protected route boundaries.
    /// </summary>
    public const string HEALTH_PREFIX = "/health";

    /// <summary>
    /// Identifies the anonymous native liveness endpoint.
    /// </summary>
    public const string LIVENESS = HEALTH_PREFIX + "/live";

    /// <summary>
    /// Identifies the anonymous database-readiness probe.
    /// </summary>
    public const string READINESS = HEALTH_PREFIX + "/ready";
}
