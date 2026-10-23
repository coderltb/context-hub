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
        // Activity events and links are immutable here. Do not export a span that cannot be scrubbed safely.
        // Standard OpenTelemetry activity export processors skip non-recorded spans.
        if (activity.Events.Any() || activity.Links.Any())
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            return;
        }

        // Instrumentation tag names evolve; an allowlist cannot accidentally expose a new sensitive tag.
        foreach (var tag in activity.TagObjects.ToArray())
        {
            if (tag.Key is not ("http.request.method" or "http.response.status_code" or "network.protocol.version"
                or "error.type" or "otel.status_code"))
            {
                activity.SetTag(tag.Key, null);
            }
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
