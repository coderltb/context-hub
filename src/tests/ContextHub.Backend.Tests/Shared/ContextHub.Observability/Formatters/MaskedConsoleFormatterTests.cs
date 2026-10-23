namespace ContextHub.Backend.Tests.Shared.ContextHub.Observability.Formatters;

/// <summary>
/// Verifies actual console output rather than only inspecting redaction helper return values.
/// </summary>
public sealed class MaskedConsoleFormatterTests
{
    /// <summary>
    /// Redacts embedded JSON in either native console format without mutating the original event property.
    /// </summary>
    /// <param name="useText">Whether the native renderer emits development text instead of compact JSON.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Format_EmbeddedJsonMasksBothNativeFormats(bool useText)
    {
        const string value = "Provider replied: {\"password\":\"fixture-secret\",\"result\":\"accepted\"} done";
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("Provider result {Result}"),
            [new LogEventProperty("Result", new ScalarValue(value))]);
        using var output = new StringWriter();

        new MaskedConsoleFormatter(useText).Format(logEvent, output);

        Assert.DoesNotContain("fixture-secret", output.ToString());
        Assert.Contains("accepted", output.ToString());
        Assert.Equal(value, ((ScalarValue)logEvent.Properties["Result"]).Value);
    }

    /// <summary>
    /// Masks structured credentials and omits exception messages without mutating the source event.
    /// </summary>
    [Fact]
    public void Format_RedactsCredentialsAndExceptionDetails()
    {
        var property = new LogEventProperty("ApiKey", new ScalarValue("credential-sentinel"));
        var exception = new InvalidOperationException("exception-sentinel");
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, exception,
            new MessageTemplateParser().Parse("Operation failed; ApiKey={ApiKey}"), [property]);
        using var output = new StringWriter();

        new MaskedConsoleFormatter().Format(logEvent, output);

        var rendered = output.ToString();
        using var document = JsonDocument.Parse(rendered);
        Assert.DoesNotContain("credential-sentinel", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("exception-sentinel", rendered, StringComparison.Ordinal);
        Assert.Equal(nameof(InvalidOperationException), document.RootElement.GetProperty("ExceptionType").GetString());
        Assert.Same(exception, logEvent.Exception);
        Assert.Equal("credential-sentinel", ((ScalarValue)logEvent.Properties["ApiKey"]).Value);
    }

    /// <summary>
    /// Keeps each multiline operational message in a single compact JSON console record.
    /// </summary>
    [Fact]
    public void Format_MultilineMessage_ProducesOneJsonRecord()
    {
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("Start\nComplete"), []);
        using var output = new StringWriter();

        new MaskedConsoleFormatter().Format(logEvent, output);

        Assert.Single(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("Start\nComplete", document.RootElement.GetProperty("@mt").GetString());
    }
}
