namespace ContextHub.Observability.Formatters;

/// <summary>
/// Delegates console rendering to native Serilog formatters after the shared masking boundary.
/// </summary>
/// <remarks>
/// Developer-owned templates remain mandatory; arbitrary free-text messages cannot be made safe by field masking.
/// </remarks>
public sealed class MaskedConsoleFormatter : ITextFormatter
{
    /// <summary>
    /// Owns the single native renderer selected for this sink.
    /// </summary>
    private readonly ITextFormatter _formatter;

    /// <summary>
    /// Selects the native renderer for one sink; Both mode composes two sinks at registration.
    /// </summary>
    /// <param name="useText">Whether this sink uses the development text format.</param>
    public MaskedConsoleFormatter(bool useText = false)
    {
        _formatter = useText
            ? new MessageTemplateTextFormatter(ObservabilityConstants.TEXT_OUTPUT_TEMPLATE, CultureInfo.InvariantCulture)
            : new CompactJsonFormatter();
    }

    /// <summary>
    /// Masks a private event copy before the native formatter writes it to either console stream.
    /// </summary>
    /// <param name="logEvent">The operational event with a developer-owned template.</param>
    /// <param name="output">The sink output writer.</param>
    public void Format(
        LogEvent logEvent,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        // Redaction owns privacy; Serilog owns quoting, timestamps, CLEF fields and text formatting.
        var maskedEvent = RedactLogEvent(logEvent);

        _formatter.Format(maskedEvent, output);
    }

    /// <summary>
    /// Copies operational properties and removes raw exception details before delegating to the native renderer.
    /// </summary>
    /// <param name="logEvent">The original Serilog event, never mutated by this formatter.</param>
    /// <returns>A private masked event for the selected output sink.</returns>
    private static LogEvent RedactLogEvent(LogEvent logEvent)
    {
        var properties = logEvent.Properties
            .Select(property => new LogEventProperty(property.Key, RedactProperty(property.Key, property.Value))).ToList();

        if (logEvent.Exception is not null)
        {
            // Exception messages, stacks and Data can contain secrets. Only the exception type is approved metadata.
            properties.RemoveAll(property => property.Name == ObservabilityConstants.EXCEPTION_TYPE);
            properties.Add(new LogEventProperty(ObservabilityConstants.EXCEPTION_TYPE, new ScalarValue(logEvent.Exception.GetType().Name)));
        }

        return new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            null,
            logEvent.MessageTemplate,
            properties,
            logEvent.TraceId ?? default,
            logEvent.SpanId ?? default);
    }

    /// <summary>
    /// Applies the shared sensitive-name authority to a native structured property.
    /// </summary>
    /// <param name="name">The native property identifier.</param>
    /// <param name="value">The structured value, never rendered before inspection.</param>
    /// <returns>A masked value or a recursively inspected operational structure.</returns>
    private static LogEventPropertyValue RedactProperty(string name, LogEventPropertyValue value) =>
        LogMaskingHelper.IsSensitiveName(name) ? new ScalarValue(SharedConstants.LoggingVariables.MASKED_VALUE) : RedactValue(value);

    /// <summary>
    /// Adapts native Serilog structures to the shared masking rules without executing arbitrary ToString methods.
    /// </summary>
    /// <param name="value">The original native structured value.</param>
    /// <returns>A sink-local value containing only inspected scalars and masked nested properties.</returns>
    private static LogEventPropertyValue RedactValue(LogEventPropertyValue value)
    {
        return value switch
        {
            ScalarValue { Value: string text } => new ScalarValue(LogMaskingHelper.MaskAllSensitiveData(text)),
            StructureValue structure => new StructureValue(structure.Properties.Select(property =>
                new LogEventProperty(property.Name, RedactProperty(property.Name, property.Value)))),
            SequenceValue sequence => new SequenceValue(sequence.Elements.Select(RedactValue)),
            DictionaryValue dictionary => new DictionaryValue(dictionary.Elements.Select(entry =>
                new KeyValuePair<ScalarValue, LogEventPropertyValue>(entry.Key, entry.Key.Value is string key ? RedactProperty(key, entry.Value) : RedactValue(entry.Value)))),
            // Unknown objects can execute arbitrary ToString code during rendering; do not forward them to a sink.
            ScalarValue { Value: not null } scalar when scalar.Value is not
                    (string or bool or char or byte or sbyte or short or ushort or int or uint or long or ulong
                    or float or double or decimal or Guid or DateTime or DateTimeOffset or TimeSpan or Enum)
                => new ScalarValue(SharedConstants.LoggingVariables.MASKED_VALUE),
            _ => value
        };
    }
}
