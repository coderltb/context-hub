namespace ContextHub.Backend.Tests.Shared.ContextHub.Observability.DependencyInjection;

/// <summary>
/// Checks host composition, native completion logging and telemetry privacy at their actual boundaries.
/// </summary>
[Collection("Process-wide telemetry")]
public sealed class ObservabilityRegistrationTests
{
    /// <summary>
    /// Honors host configuration that disables tracing without disabling HTTP request logging.
    /// </summary>
    [Fact]
    public void Telemetry_DisabledTracing_DoesNotRegisterTracerProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Telemetry:TracingEnabled"] = "false",
            ["Observability:Telemetry:MetricsEnabled"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddTelemetry(configuration, "TestService");
        using var container = services.BuildServiceProvider();

        Assert.Null(container.GetService<TracerProvider>());
        Assert.Null(container.GetService<MeterProvider>());
    }

    /// <summary>
    /// Disables metrics independently while preserving application tracing and its privacy processor.
    /// </summary>
    [Fact]
    public void Telemetry_DisabledMetrics_PreservesTracing()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Telemetry:MetricsEnabled"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddTelemetry(configuration, "TestService");
        using var container = services.BuildServiceProvider();

        Assert.NotNull(container.GetService<TracerProvider>());
        Assert.Null(container.GetService<MeterProvider>());
    }

    /// <summary>
    /// Preserves operational metrics when the host disables distributed tracing.
    /// </summary>
    [Fact]
    public void Telemetry_DisabledTracing_PreservesMetrics()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Telemetry:TracingEnabled"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddTelemetry(configuration, "TestService");
        using var container = services.BuildServiceProvider();

        Assert.Null(container.GetService<TracerProvider>());
        Assert.NotNull(container.GetService<MeterProvider>());
    }

    /// <summary>
    /// Rejects unbounded filters instead of accepting unlimited configuration into every request callback.
    /// </summary>
    [Fact]
    public void Telemetry_TooManyExcludedPaths_IsRejected()
    {
        var entries = Enumerable.Range(0, 33)
            .ToDictionary(index => $"Observability:Telemetry:ExcludedServerPaths:{index}", _ => (string?)"/health");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(entries).Build();

        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddTelemetry(configuration, "TestService"));
    }

    /// <summary>
    /// Filters only the configured path segment, preserving similarly named application endpoints.
    /// </summary>
    /// <param name="excludedFirst">Whether the filtered request precedes the traced request.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Telemetry_ExcludedPath_DoesNotExcludeSimilarlyNamedRoute(bool excludedFirst)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Telemetry:ExcludedServerPaths:0"] = "/health"
        }).Build();
        var exporter = new CapturingExporter();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddTelemetry(configuration, "TestService")
            .WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(exporter)));
        await using var app = builder.Build();
        app.Run(_ => Task.CompletedTask);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        var paths = excludedFirst ? new[] { "/health/ready", "/healthy" } : ["/healthy", "/health/ready"];
        foreach (var path in paths)
        {
            // Each synthetic request owns its sampled parent; the host receives an actual loopback HTTP request.
            using var parent = new Activity("Test request").Start();
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
            using var response = await client.GetAsync(path);
        }

        var span = Assert.Single(exporter.Spans, activity => activity.Kind == ActivityKind.Server);
        Assert.Equal(ObservabilityConstants.SERVER_OPERATION, span.DisplayName);
        Assert.Equal(2, exporter.Spans.Count(activity => activity.Kind == ActivityKind.Client));
    }

    /// <summary>
    /// Rejects a malformed configured path before constructing diagnostic services.
    /// </summary>
    /// <param name="path">The request path or configured path exercised by this scenario.</param>
    [Theory]
    [InlineData("relative-path")]
    [InlineData("/health?token=private")]
    [InlineData("/")]
    [InlineData("///")]
    public void Telemetry_InvalidExcludedPath_IsRejectedWithoutEcho(string path)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Telemetry:ExcludedServerPaths:0"] = path
        }).Build();

        var exception = Assert.Throws<ArgumentException>(() => new ServiceCollection()
            .AddTelemetry(configuration, "TestService"));

        Assert.StartsWith(ObservabilityErrorConstants.INVALID_EXCLUDED_PATHS, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rejects invalid format configuration when the host resolves its logging services.
    /// </summary>
    [Fact]
    public void Logging_InvalidConsoleFormat_IsRejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Logging:ConsoleFormat"] = "Unknown"
        }).Build();
        var services = new ServiceCollection();
        services.AddLoggingInfrastructure(configuration, "TestService", "Test");
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<LoggingOptions>>().Value);
    }

    /// <summary>
    /// Emits one completion at the approved severity without collecting URL, query, body or credentials.
    /// </summary>
    /// <param name="status">The synthetic endpoint status.</param>
    /// <param name="level">The expected completion severity.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData(200, LogEventLevel.Information)]
    [InlineData(403, LogEventLevel.Warning)]
    [InlineData(500, LogEventLevel.Error)]
    public async Task RequestLogging_UsesSafeCompletionProperties(int status, LogEventLevel level)
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSerilog(logger, dispose: false);
        await using var app = builder.Build();
        app.UseRequestLogging();
        app.Run(context =>
        {
            context.Response.StatusCode = status;
            return Task.CompletedTask;
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/private-path?token=private-query");

        // HTTP response delivery can precede completion logging; wait for the sink boundary rather than sleeping.
        await sink.RequestCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        var completion = Assert.Single(sink.Events, entry => entry.MessageTemplate.Text == ObservabilityLogConstants.REQUEST_COMPLETED);
        Assert.Equal(level, completion.Level);
        Assert.Equal(status, ((ScalarValue)completion.Properties[ObservabilityConstants.STATUS_CODE]).Value);
        using var output = new StringWriter();
        new MaskedConsoleFormatter().Format(completion, output);
        Assert.DoesNotContain("private", output.ToString(), StringComparison.Ordinal);
        Assert.False(completion.Properties.ContainsKey("RequestPath"));
    }

    /// <summary>
    /// Removes sensitive tags and description before a standard activity exporter observes the span.
    /// </summary>
    [Fact]
    public void Telemetry_ExportsOnlyAllowlistedMetadata()
    {
        var exporter = new CapturingExporter();
        var services = new ServiceCollection();
        services.AddTelemetry("TestService")
            .WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(exporter)));
        using var container = services.BuildServiceProvider();
        _ = container.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(ObservabilityConstants.ACTIVITY_SOURCE);
        using (var activity = source.StartActivity("private-url", ActivityKind.Client))
        {
            Assert.NotNull(activity);
            activity.SetTag("url.full", "https://private.invalid/?token=secret");
            activity.SetTag("http.response.status_code", 200);
            activity.SetStatus(ActivityStatusCode.Error, "private-description");
        }

        var span = Assert.Single(exporter.Spans);
        Assert.Equal(ObservabilityConstants.CLIENT_OPERATION, span.DisplayName);
        Assert.Null(span.GetTagItem("url.full"));
        Assert.Equal(200, span.GetTagItem("http.response.status_code"));
        Assert.Null(span.StatusDescription);
    }

    /// <summary>
    /// Suppresses spans whose immutable events cannot be safely rewritten before export.
    /// </summary>
    [Fact]
    public void Telemetry_PrivateEvent_IsNotExported()
    {
        var exporter = new CapturingExporter();
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource("Test.PrivateSpan")
            .AddProcessor(new SafeActivityProcessor())
            .AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();
        using var source = new ActivitySource("Test.PrivateSpan");
        using (var activity = source.StartActivity("Operation"))
        {
            Assert.NotNull(activity);
            activity.AddEvent(new ActivityEvent("private-content"));
        }

        Assert.Empty(exporter.Spans);
    }

    /// <summary>
    /// Captures native Serilog events without installing a global logger or redirecting stdout.
    /// </summary>
    private sealed class CapturingSink : ILogEventSink
    {
        /// <summary>
        /// Signals that the native completion event has reached this test sink.
        /// </summary>
        private readonly TaskCompletionSource _requestCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets operational events emitted by the synthetic host.
        /// </summary>
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        /// <summary>
        /// Gets the asynchronous request-completion boundary observed by the sink.
        /// </summary>
        public Task RequestCompleted => _requestCompleted.Task;

        /// <summary>
        /// Records a native event for boundary assertions.
        /// </summary>
        /// <param name="logEvent">The event received by the sink.</param>
        public void Emit(LogEvent logEvent)
        {
            Events.Enqueue(logEvent);
            if (logEvent.MessageTemplate.Text == ObservabilityLogConstants.REQUEST_COMPLETED)
            {
                _requestCompleted.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Captures only spans accepted by the real OpenTelemetry export processor.
    /// </summary>
    private sealed class CapturingExporter : BaseExporter<Activity>
    {
        /// <summary>
        /// Gets activities delivered to the exporter.
        /// </summary>
        public ConcurrentQueue<Activity> Spans { get; } = new();

        /// <summary>
        /// Records an exported batch without network or external collector access.
        /// </summary>
        /// <param name="batch">The accepted export batch.</param>
        /// <returns>A successful in-memory export result.</returns>
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var span in batch)
            {
                Spans.Enqueue(span);
            }

            return ExportResult.Success;
        }
    }
}

/// <summary>
/// Isolates tests that install process-wide diagnostic listeners from concurrently executing HTTP test hosts.
/// </summary>
[CollectionDefinition("Process-wide telemetry", DisableParallelization = true)]
public sealed class TelemetryCollectionDefinition;
