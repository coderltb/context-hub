namespace ContextHub.Observability.Processors;

/// <summary>
/// Removes request content and exception details from finished spans before approved export processors run.
/// </summary>
public sealed class SafeActivityProcessor : BaseProcessor<Activity>
{
    /// <summary>
    /// Keeps only allowlisted operational tags; request URLs, headers, queries and custom content are excluded.
    /// </summary>
    /// <param name="activity">The completed operation.</param>
    public override void OnEnd(Activity activity)
    {
        // Skip immutable spans that cannot be scrubbed safely; native exporters respect the recording flag.
        if (activity.Events.Any() || activity.Links.Any())
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            return;
        }

        // Instrumentation tag names evolve; an allowlist cannot accidentally expose a new sensitive tag.
        foreach (var tag in activity.TagObjects.Where(tag => tag.Key is not
            (ObservabilityConstants.TelemetryTags.HTTP_METHOD
            or ObservabilityConstants.TelemetryTags.HTTP_STATUS
            or ObservabilityConstants.TelemetryTags.PROTOCOL_VERSION
            or ObservabilityConstants.TelemetryTags.ERROR_TYPE
            or ObservabilityConstants.TelemetryTags.OTEL_STATUS_CODE)).ToArray())
        {
            activity.SetTag(tag.Key, null);
        }

        // HttpClient display names may contain caller-controlled values. Export an operation class, not a URL.
        activity.DisplayName = activity.Kind switch
        {
            ActivityKind.Server => ObservabilityConstants.SERVER_OPERATION,
            ActivityKind.Client => ObservabilityConstants.CLIENT_OPERATION,
            _ => ObservabilityConstants.APPLICATION_OPERATION
        };
        activity.SetStatus(activity.Status);
    }
}
