namespace ContextHub.ApiCommon.Constants;

/// <summary>
/// Defines stable probe labels and native health output values, without dependency configuration.
/// </summary>
public static class HealthConstants
{
    /// <summary>
    /// Identifies the process-only check, never a dependency readiness probe.
    /// </summary>
    public const string SELF = "self";

    /// <summary>
    /// Selects process liveness independently from infrastructure.
    /// </summary>
    public const string LIVENESS_TAG = "live";

    /// <summary>
    /// Selects required service-owned dependency probes for readiness.
    /// </summary>
    public const string READINESS_TAG = "ready";

    /// <summary>
    /// Represents all required checks completing successfully.
    /// </summary>
    public const string HEALTHY = "Healthy";

    /// <summary>
    /// Represents failed, degraded or unconfigured readiness without diagnostic detail.
    /// </summary>
    public const string UNHEALTHY = "Unhealthy";
}
