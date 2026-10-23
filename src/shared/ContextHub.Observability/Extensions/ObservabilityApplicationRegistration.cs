namespace ContextHub.Observability.Extensions;

/// <summary>
/// Adds explicitly selected HTTP logging to a host pipeline without changing protocol handling.
/// </summary>
public static class ObservabilityApplicationRegistration
{
    /// <summary>
    /// Adds one native Serilog completion event using a safe property allowlist and host-owned logger.
    /// </summary>
    /// <param name="app">The API host pipeline, outside the exception adapter and before endpoints.</param>
    /// <returns>The same pipeline for composition.</returns>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseSerilogRequestLogging(options =>
        {
            // Bind to this host's logger; the Serilog static logger can belong to an unrelated API host.
            options.Logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
            options.IncludeQueryInRequestPath = false;
            options.MessageTemplate = ObservabilityLogConstants.REQUEST_COMPLETED;
            options.GetLevel = (context, _, exception) => context.RequestAborted.IsCancellationRequested
                ? LogEventLevel.Warning
                : exception is not null || context.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : context.Response.StatusCode >= 400 ? LogEventLevel.Warning : LogEventLevel.Information;
            options.GetMessageTemplateProperties = (context, _, elapsed, status) =>
            [
                // Replace native path properties entirely: even a path segment can contain private input.
                new LogEventProperty(ObservabilityConstants.REQUEST_METHOD, new ScalarValue(SafeMethod(context.Request.Method))),
                new LogEventProperty(ObservabilityConstants.STATUS_CODE, new ScalarValue(status)),
                new LogEventProperty(ObservabilityConstants.ELAPSED, new ScalarValue(elapsed))
            ];
        });
    }

    /// <summary>
    /// Prevents an unknown caller-supplied HTTP method from becoming an arbitrary log string.
    /// </summary>
    /// <param name="method">The inbound HTTP method.</param>
    /// <returns>A standard method or a content-neutral fallback.</returns>
    private static string SafeMethod(string method)
    {
        return method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" or "CONNECT" or "TRACE"
            ? method
            : ObservabilityConstants.UNKNOWN_METHOD;
    }
}
