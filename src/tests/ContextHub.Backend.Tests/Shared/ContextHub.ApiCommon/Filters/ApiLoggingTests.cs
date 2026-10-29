namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Filters;

/// <summary>
/// Verifies bound DTO logging through the real MVC adapters without relying on console masking to protect credentials.
/// </summary>
public sealed class ApiLoggingTests
{
    /// <summary>
    /// Captures masked arguments and the executed response while retaining the original client-visible DTO.
    /// </summary>
    /// <returns>The pre-logger masking, final status and metadata assertions.</returns>
    [Fact]
    public async Task BoundDtoAndFinalResponseAreMaskedBeforeLogger()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/logging/v1/echo", new
        {
            label = "safe-label", password = "request-password", token = "response-token",
            invitationUrl = "https://example.test/invite?%74oken=invitation-secret",
            child = new { password = "nested-secret", label = "safe-child" },
            children = new[] { new { label = "safe-collection", token = "collection-secret" } }
        });
        using var wire = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("response-token", wire.RootElement.GetProperty("data").GetProperty("token").GetString());
        var received = Assert.Single(logs.Entries, entry => entry.Properties.ContainsKey("RequestPayload"));
        var produced = Assert.Single(logs.Entries, entry => entry.Properties.ContainsKey("ResponsePayload"));
        var text = received.Message + produced.Message;
        foreach (var secret in new[] { "request-password", "response-token", "invitation-secret", "nested-secret", "collection-secret" })
        {
            Assert.DoesNotContain(secret, text);
        }

        Assert.Contains("safe-label", received.Message);
        Assert.Contains("safe-child", produced.Message);
        Assert.Contains("safe-collection", produced.Message);
        Assert.Equal(201, produced.Properties["StatusCode"]);
        Assert.Contains("9.2", produced.Message);
        Assert.DoesNotContain(received.Properties.Values, value => value is LoggingProbeRequest);
    }

    /// <summary>
    /// Records native automatic validation without requiring the action or its handler to execute.
    /// </summary>
    /// <param name="apiFirst">Whether shared registration precedes native MVC registration.</param>
    /// <returns>The validation status, envelope and single-event assertions.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AutomaticBadRequestLogsFinalEnvelopeOnce(bool apiFirst)
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs, apiFirst);
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/logging/v1/echo", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(logs.Entries, entry => entry.Properties.ContainsKey("RequestPayload"));
        var produced = Assert.Single(logs.Entries, entry => entry.Properties.ContainsKey("ResponsePayload"));
        Assert.Equal(400, produced.Properties["StatusCode"]);
        Assert.Contains("error_validation", produced.Message);
    }

    /// <summary>
    /// Leaves non-boundary endpoints outside payload logging even when they return the same DTO shape.
    /// </summary>
    /// <returns>The absence of out-of-boundary payload diagnostics.</returns>
    [Fact]
    public async Task OutsideBoundaryDoesNotLogPayload()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/outside/logging-probe", new { label = "safe", password = "outside-secret" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(logs.Entries);
    }

    /// <summary>
    /// Does not log a bound Authorization header even when its CLR parameter name is not a sensitive keyword.
    /// </summary>
    /// <returns>The transport-header exclusion assertions.</returns>
    [Fact]
    public async Task BoundHeaderValueIsExcludedBeforeSerialization()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "header-credential");
        using var response = await client.GetAsync("/api/logging/v1/headers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("header-credential", string.Join("\n", logs.Entries.Select(entry => entry.Message)));
    }

    /// <summary>
    /// Observes the mapped envelope after a native bodyless error result has been adapted.
    /// </summary>
    /// <returns>The executed status/error metadata assertions.</returns>
    [Fact]
    public async Task BodylessErrorLogsMappedResult()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/logging/v1/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var produced = Assert.Single(logs.Entries, entry => entry.Properties.ContainsKey("ResponsePayload"));
        Assert.Equal(404, produced.Properties["StatusCode"]);
        Assert.Contains("error_not_found", produced.Message);
    }

    /// <summary>
    /// Preserves a real global error response without inventing a successful MVC response event.
    /// </summary>
    /// <returns>The exception ownership assertions.</returns>
    [Fact]
    public async Task GlobalExceptionDoesNotEmitSuccessfulResultPayload()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/logging/v1/failure");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Single(logs.Entries, entry => entry.Properties.ContainsKey("RequestPayload"));
        Assert.DoesNotContain(logs.Entries, entry => entry.Properties.ContainsKey("ResponsePayload"));
        Assert.DoesNotContain("private-failure", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Limits only diagnostics while leaving the original oversized DTO available to the client.
    /// </summary>
    /// <returns>The bounded log and unchanged wire assertions.</returns>
    [Fact]
    public async Task OversizedDtoUsesMetadataOnly()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var client = app.GetTestClient();
        var label = new string('z', 17_000);
        using var response = await client.PostAsJsonAsync("/api/logging/v1/echo", new { label });
        using var wire = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(label, wire.RootElement.GetProperty("data").GetProperty("label").GetString());
        Assert.Equal(2, logs.Entries.Count);
        foreach (var entry in logs.Entries)
        {
            Assert.Equal("size_limit", entry.Properties["PayloadOmittedReason"]);
            Assert.DoesNotContain(label, entry.Message);
        }
    }

    /// <summary>
    /// Applies the size cap after replacing credentials, not to the raw secret-bearing JSON.
    /// </summary>
    /// <returns>The redaction-before-limit assertions.</returns>
    [Fact]
    public async Task LargeSecretCanBeLoggedOnlyAfterRedaction()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var client = app.GetTestClient();
        var secret = new string('q', 20_000);
        using var response = await client.PostAsJsonAsync("/api/logging/v1/echo", new { label = "safe", password = secret, alias = "public-alias" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        foreach (var entry in logs.Entries)
        {
            Assert.Null(entry.Properties["PayloadOmittedReason"]);
            Assert.DoesNotContain(secret, entry.Message);
            Assert.Contains("visible_alias", entry.Message);
            Assert.Contains("public-alias", entry.Message);
        }
    }

    /// <summary>
    /// Captures only safe diagnostic metadata when a getter is unsupported by the diagnostic serializer.
    /// </summary>
    /// <returns>The successful wire and omitted diagnostic assertions.</returns>
    [Fact]
    public async Task DiagnosticSerializerFailureDoesNotChangeResponse()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/logging/v1/serializer-failure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var produced = Assert.Single(logs.Entries, entry => entry.Properties.ContainsKey("ResponsePayload"));
        Assert.Equal("serialization_failure", produced.Properties["PayloadOmittedReason"]);
        Assert.DoesNotContain("getter-private-value", string.Join("\n", logs.Entries.Select(entry => entry.Message)));
    }

    /// <summary>
    /// Keeps the two DTO diagnostics separate from the single native HTTP completion event.
    /// </summary>
    /// <param name="path">The executed MVC success, validation or exception route.</param>
    /// <param name="status">The final status observed by the native completion middleware.</param>
    /// <param name="responseEvents">Whether MVC executed a response result rather than the global handler.</param>
    /// <returns>The event ownership and cardinality assertions.</returns>
    [Theory]
    [InlineData("/api/logging/v1/echo", 201, 1)]
    [InlineData("/api/logging/v1/missing", 404, 1)]
    [InlineData("/api/logging/v1/failure", 500, 0)]
    public async Task PayloadEventsDoNotDuplicateHttpCompletion(string path, int status, int responseEvents)
    {
        var sink = new CompletionSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs, transportLogger: logger);
        using var client = app.GetTestClient();
        using var response = path.EndsWith("/echo", StringComparison.Ordinal)
            ? await client.PostAsJsonAsync(path, new { label = "safe", token = "completion-secret" })
            : await client.GetAsync(path);

        // Response delivery can precede middleware completion; synchronize with its sink event instead of sleeping.
        await sink.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var completion = Assert.Single(sink.Events,
            entry => entry.MessageTemplate.Text == ObservabilityLogConstants.REQUEST_COMPLETED);
        Assert.Equal(status, ((ScalarValue)completion.Properties[ObservabilityConstants.STATUS_CODE]).Value);
        Assert.Single(sink.Events, entry => entry.Properties.ContainsKey("RequestPayload"));
        Assert.Equal(responseEvents, sink.Events.Count(entry => entry.Properties.ContainsKey("ResponsePayload")));
        Assert.DoesNotContain("completion-secret", string.Join("\n", sink.Events.Select(entry => entry.RenderMessage())));
    }

    /// <summary>
    /// Leaves authorization and routing failures to native transport diagnostics before MVC can bind DTOs.
    /// </summary>
    /// <param name="path">The protected or unmapped path.</param>
    /// <param name="authenticated">Whether a fixture principal lacking the required role is supplied.</param>
    /// <param name="status">The expected safe native failure status.</param>
    /// <returns>The single completion and absent DTO payload assertions.</returns>
    [Theory]
    [InlineData("/api/logging/v1/protected", false, 401)]
    [InlineData("/api/logging/v1/protected", true, 403)]
    [InlineData("/api/logging/v1/unmapped", false, 404)]
    public async Task BeforeMvcFailuresRetainTransportDiagnostics(string path, bool authenticated, int status)
    {
        var sink = new CompletionSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        await using var app = await CreateHost(new PayloadLogProvider(), transportLogger: logger);
        using var client = app.GetTestClient();
        if (authenticated)
        {
            client.DefaultRequestHeaders.Add("X-Fixture-Authenticated", "true");
        }
        using var response = await client.GetAsync(path);

        await sink.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Single(sink.Events, entry => entry.MessageTemplate.Text == ObservabilityLogConstants.REQUEST_COMPLETED);
        Assert.DoesNotContain(sink.Events, entry => entry.Properties.ContainsKey("RequestPayload") || entry.Properties.ContainsKey("ResponsePayload"));
    }

    /// <summary>
    /// Checks native scope validation, one filter wrapper and reuse of an existing JSON registration.
    /// </summary>
    /// <returns>The container and filter-order assertions.</returns>
    [Fact]
    public async Task RegistrationReusesSerializerAndAddsOneOrderedWrapper()
    {
        var logs = new PayloadLogProvider();
        await using var app = await CreateHost(logs);
        using var scope = app.Services.CreateScope();
        var serializer = app.Services.GetRequiredService<IJsonSerializerService>();
        Assert.Same(serializer, scope.ServiceProvider.GetRequiredService<IJsonSerializerService>());
        var filters = app.Services.GetRequiredService<IOptions<MvcOptions>>().Value.Filters;
        var wrapper = Assert.Single(filters.OfType<TypeFilterAttribute>(), filter => filter.ImplementationType == typeof(ApiLoggingFilter));
        Assert.Equal(-2002, wrapper.Order);
        var filter = Assert.IsType<ApiLoggingFilter>(wrapper.CreateInstance(scope.ServiceProvider));
        Assert.Equal(wrapper.Order, filter.Order);
    }

    /// <summary>
    /// Starts native MVC and shared response adapters with a request-local logger provider.
    /// </summary>
    /// <param name="logs">The test-owned capture boundary before console formatting.</param>
    /// <param name="apiFirst">Whether shared API registration precedes native MVC setup.</param>
    /// <param name="transportLogger">Optional test-owned logger for the native HTTP completion middleware.</param>
    /// <returns>The started HTTP test host.</returns>
    private static async Task<WebApplication> CreateHost(
        PayloadLogProvider logs,
        bool apiFirst = true,
        Serilog.ILogger? transportLogger = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ApiLoggingProbeController).Assembly.GetName().Name,
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        if (transportLogger is not null)
        {
            builder.Services.AddSerilog(transportLogger, dispose: false);
        }

        builder.Services.AddJsonSerializer();
        var jsonDescriptor = Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IJsonSerializerService));
        if (apiFirst)
        {
            builder.Services.AddApiInfrastructure("/api/logging/v1", "9.2");
        }

        builder.Services.AddControllers().AddApplicationPart(typeof(ApiLoggingProbeController).Assembly);
        builder.Services.AddAuthentication("logging-fixture")
            .AddScheme<AuthenticationSchemeOptions, LoggingFixtureAuthenticationHandler>("logging-fixture", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("logging-owner", policy => policy.RequireRole("Owner")));
        if (!apiFirst)
        {
            builder.Services.AddApiInfrastructure("/api/logging/v1", "9.2");
        }
        Assert.Same(jsonDescriptor,
            Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IJsonSerializerService)));
        var app = builder.Build();
        app.UseCorrelation();
        if (transportLogger is not null)
        {
            app.UseRequestLogging();
        }

        app.UseApiErrorHandling();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        return app;
    }

    /// <summary>
    /// Retains only API payload events to prove protection before any Serilog sink or formatter runs.
    /// </summary>
    private sealed class PayloadLogProvider : ILoggerProvider
    {
        /// <summary>
        /// Gets captured structured properties and rendered messages in thread-safe arrival order.
        /// </summary>
        public ConcurrentQueue<PayloadEntry> Entries { get; } = new();

        /// <summary>
        /// Creates a category-aware capture logger.
        /// </summary>
        /// <param name="categoryName">The framework-supplied category.</param>
        /// <returns>The capture logger.</returns>
        public MicrosoftLogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Entries);

        /// <summary>
        /// Leaves the managed queue to its test owner.
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// Captures scalars without destructuring the original DTO or exception.
        /// </summary>
        /// <param name="category">The owning category.</param>
        /// <param name="entries">The test-owned destination.</param>
        private sealed class CaptureLogger(string category, ConcurrentQueue<PayloadEntry> entries) : MicrosoftLogger
        {
            /// <summary>
            /// Uses the existing no-op scope implementation.
            /// </summary>
            /// <typeparam name="TState">The scope state type.</typeparam>
            /// <param name="state">The scope state.</param>
            /// <returns>The no-op scope.</returns>
            public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
                NullLogger.Instance.BeginScope(state);

            /// <summary>
            /// Enables capture independent of the console threshold.
            /// </summary>
            /// <param name="logLevel">The diagnostic severity.</param>
            /// <returns>True for every level.</returns>
            public bool IsEnabled(LogLevel logLevel) => true;

            /// <summary>
            /// Captures only the new filter category before renderer-level redaction could hide a defect.
            /// </summary>
            /// <typeparam name="TState">The structured log state.</typeparam>
            /// <param name="logLevel">The event severity.</param>
            /// <param name="eventId">The event identifier.</param>
            /// <param name="state">The structured scalar properties.</param>
            /// <param name="exception">The optional diagnostic exception.</param>
            /// <param name="formatter">The native message formatter.</param>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (category.EndsWith(".ApiLoggingFilter", StringComparison.Ordinal))
                {
                    var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                        ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                        : new Dictionary<string, object?>();
                    entries.Enqueue(new PayloadEntry(formatter(state, exception), properties));
                }
            }
        }
    }

    /// <summary>
    /// Observes native completion without installing a process-wide logger or replacing console output.
    /// </summary>
    private sealed class CompletionSink : ILogEventSink
    {
        /// <summary>
        /// Gets the event boundary used to await native middleware completion.
        /// </summary>
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets the structured native events emitted by this host.
        /// </summary>
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        /// <summary>
        /// Captures an event and signals only the HTTP completion event.
        /// </summary>
        /// <param name="logEvent">The event emitted to the test-owned sink.</param>
        public void Emit(LogEvent logEvent)
        {
            Events.Enqueue(logEvent);
            if (logEvent.MessageTemplate.Text == ObservabilityLogConstants.REQUEST_COMPLETED)
            {
                Completed.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Preserves the pre-formatter event for assertions.
    /// </summary>
    /// <param name="Message">The rendered diagnostic.</param>
    /// <param name="Properties">The original structured scalar values.</param>
    private sealed record PayloadEntry(string Message, IReadOnlyDictionary<string, object?> Properties);
}

/// <summary>
/// Supplies controller results without business dependencies or product database access.
/// </summary>
[ApiController]
[Route("api/logging/v1")]
public sealed class ApiLoggingProbeController : ControllerBase
{
    /// <summary>
    /// Returns the submitted DTO so tests can prove logging never mutates client-visible secrets.
    /// </summary>
    /// <param name="request">The model-bound test DTO.</param>
    /// <param name="cancellationToken">The framework-bound cancellation parameter that must not be serialized.</param>
    /// <returns>The created DTO envelope.</returns>
    [HttpPost("echo")]
    [HttpPost("/outside/logging-probe")]
    public IActionResult Echo(LoggingProbeRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, new ApiResponse<LoggingProbeRequest>(request));

    /// <summary>
    /// Accepts a header only to demonstrate that it is not an ordinary payload parameter.
    /// </summary>
    /// <param name="input">The bound native header, deliberately named without a masking keyword.</param>
    /// <returns>The safe independent DTO.</returns>
    [HttpGet("headers")]
    public IActionResult Header([FromHeader(Name = "Authorization")] string? input) =>
        Ok(new ApiResponse<LoggingProbeRequest>(new LoggingProbeRequest { Label = "safe-header-result" }));

    /// <summary>
    /// Exposes a protected route whose challenge/forbid occurs before MVC action execution.
    /// </summary>
    /// <returns>The safe DTO only when its role policy succeeds.</returns>
    [Authorize(Policy = "logging-owner")]
    [HttpGet("protected")]
    public IActionResult Protected() => Ok(new ApiResponse<LoggingProbeRequest>(new LoggingProbeRequest { Label = "protected" }));

    /// <summary>
    /// Supplies a bodyless error for native API adaptation.
    /// </summary>
    /// <returns>The not-found result.</returns>
    [HttpGet("missing")]
    public IActionResult Missing() => NotFound();

    /// <summary>
    /// Supplies an unhandled application failure for the existing safe global handler.
    /// </summary>
    /// <returns>No result because the failure propagates.</returns>
    [HttpGet("failure")]
    public IActionResult Failure() => throw new InvalidOperationException("private-failure");

    /// <summary>
    /// Lets the native wire serializer ignore a diagnostic-only failing getter.
    /// </summary>
    /// <returns>The safe DTO response.</returns>
    [HttpGet("serializer-failure")]
    public IActionResult SerializerFailure() => Ok(new ApiResponse<LoggingGetterFailureDto>(new LoggingGetterFailureDto()));

}

/// <summary>
/// Provides nested credential and safe-value cases using an ordinary model-bound DTO.
/// </summary>
public sealed class LoggingProbeRequest
{
    /// <summary>
    /// Gets or sets the required safe display value.
    /// </summary>
    [Required]
    public string Label { get; set; } = "";

    /// <summary>
    /// Gets or sets a credential that may be returned to this test client but never to a logger.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets a one-time token used to detect mutation or leakage.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets an invitation URL with a credential query parameter.
    /// </summary>
    public string? InvitationUrl { get; set; }

    /// <summary>
    /// Gets or sets nested values whose credentials need recursive masking.
    /// </summary>
    public LoggingProbeRequest? Child { get; set; }

    /// <summary>
    /// Gets or sets materialized children whose credentials need recursive masking.
    /// </summary>
    public IReadOnlyList<LoggingProbeRequest>? Children { get; set; }

    /// <summary>
    /// Gets or sets a safe alias honored by the existing diagnostic JSON contract.
    /// </summary>
    [NewtonsoftJsonProperty("visible_alias")]
    public string? Alias { get; set; }
}

/// <summary>
/// Distinguishes diagnostic serialization failure from a successful native wire result.
/// </summary>
public sealed class LoggingGetterFailureDto
{
    /// <summary>
    /// Gets the client-visible data.
    /// </summary>
    public string Value => "safe";

    /// <summary>
    /// Throws only if a diagnostic serializer evaluates this getter; the native response contract excludes it.
    /// </summary>
    [SystemTextJsonIgnore]
    public string Broken => throw new InvalidOperationException("getter-private-value");
}
