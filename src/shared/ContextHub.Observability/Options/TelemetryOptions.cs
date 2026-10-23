namespace ContextHub.Observability.Options;

/// <summary>
/// Controls native instrumentation without accepting exporter credentials, payload capture or exception recording.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>
    /// Gets or sets whether this registration creates tracing services; exporters are configured separately by the host.
    /// </summary>
    public bool TracingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this registration creates metric services, independently of tracing and stdout logging.
    /// </summary>
    public bool MetricsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets bounded non-root path segments excluded from server traces; this does not hide HTTP completion logs or metrics.
    /// </summary>
    public string[] ExcludedServerPaths { get; set; } = [];
}
