namespace ContextHub.Application.Exceptions.Telemetry;

/// <summary>
/// Reports a narrow feature-boundary outcome without an HTTP dependency.
/// </summary>
public sealed class QualitySignalRateLimitException() : Exception("The quality signal budget is exhausted.")
{
    /// <summary>
    /// Gets the fixed counter window in seconds.
    /// </summary>
    public int RetryAfterSeconds => 3600;
}
