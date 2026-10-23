namespace ContextHub.Observability.Enrichers;

/// <summary>
/// Adds W3C trace identifiers without copying arbitrary activity tags or baggage into logs.
/// </summary>
public sealed class ActivityEnricher : ILogEventEnricher
{
    /// <summary>
    /// Enriches an event only when the executing operation has a W3C activity.
    /// </summary>
    /// <param name="logEvent">The operational event.</param>
    /// <param name="propertyFactory">The Serilog property factory.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity?.IdFormat != ActivityIdFormat.W3C)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ObservabilityConstants.TRACE_ID, activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ObservabilityConstants.SPAN_ID, activity.SpanId.ToString()));
    }
}
