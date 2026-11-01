namespace ContextHub.Infrastructure.Constants.Telemetry;

/// <summary>
/// Contains safe feature event templates without raw request or provider data.
/// </summary>
internal static class TelemetryLogConstants
{
    /// <summary>
    /// Records a known quality-signal provider outage without arguments, keys or submitted codes.
    /// </summary>
    internal const string QUALITY_DEPENDENCY_UNAVAILABLE = "Quality signal dependency is unavailable. FailureType={FailureType}";

    /// <summary>
    /// Records an optimistic write conflict without logging a concurrency proof.
    /// </summary>
    internal const string CONCURRENCY_REJECTED = "Context Hub optimistic update rejected for {Operation}.";
}
