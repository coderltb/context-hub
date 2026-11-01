namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Filters;

/// <summary>
/// Verifies diagnostic isolation at MVC filter boundaries using the registered serializer and native continuations.
/// </summary>
public sealed class ApiLoggingFilterTests
{
    /// <summary>
    /// Explicit metadata-only endpoints never evaluate private DTO getters in either diagnostic phase.
    /// </summary>
    /// <param name="resultStage">Whether the response rather than request is observed.</param>
    /// <returns>The untouched payload and omission diagnostic.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataOnlyEndpointDoesNotInspectPayload(bool resultStage)
    {
        using var fixture = new FilterFixture();
        var payload = new ThrowingGetterPayload();
        if (resultStage)
        {
            var context = CreateResultContext(payload);
            context.ActionDescriptor.EndpointMetadata = [new ApiMetadataOnlyLoggingAttribute()];
            await fixture.Filter.OnResultExecutionAsync(context,
                () => Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller)));
        }
        else
        {
            var context = CreateActionContext(new Dictionary<string, object?> { ["request"] = payload });
            context.ActionDescriptor.EndpointMetadata = [new ApiMetadataOnlyLoggingAttribute()];
            await fixture.Filter.OnActionExecutionAsync(context,
                () => Task.FromResult(new ActionExecutedContext(context, [], context.Controller)));
        }

        Assert.Equal(0, payload.ReadCount);
        var entry = Assert.Single(fixture.Logs.Entries);
        Assert.Contains(ApiCommonConstants.Logging.METADATA_ONLY, entry.Message);
        Assert.DoesNotContain("private-getter-detail", entry.Message);
    }

    /// <summary>
    /// Logs only request-bound DTO values and leaves framework objects, services, transport bodies and request state alone.
    /// </summary>
    /// <returns>The argument-selection and ownership assertions.</returns>
    [Fact]
    public async Task RequestSkipsServicesAndFrameworkArgumentsWithoutRetainingSnapshots()
    {
        using var fixture = new FilterFixture();
        using var body = new ProbeStream();
        var service = new ThrowingGetterPayload();
        var context = CreateActionContext(new Dictionary<string, object?>
        {
            ["request"] = new DiagnosticPayload("visible-request", "request-secret"),
            ["service"] = service,
            ["cancellationToken"] = CancellationToken.None,
            ["httpContext"] = new DefaultHttpContext(),
            ["principal"] = new ClaimsPrincipal(),
            ["services"] = fixture.Services
        });
        context.ActionDescriptor.Parameters =
        [
            new ParameterDescriptor
            {
                Name = "service", ParameterType = typeof(ThrowingGetterPayload),
                BindingInfo = new BindingInfo { BindingSource = BindingSource.Services }
            }
        ];
        context.HttpContext.Request.Body = body;
        var marker = new object();
        context.HttpContext.Items["existing"] = marker;
        var invoked = false;

        await fixture.Filter.OnActionExecutionAsync(context, () =>
        {
            invoked = true;

            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });

        Assert.True(invoked);
        var entry = Assert.Single(fixture.Logs.Entries);
        Assert.Contains("visible-request", entry.Message);
        Assert.DoesNotContain("request-secret", entry.Message);
        foreach (var name in new[] { "service", "cancellationToken", "httpContext", "principal", "services" })
        {
            Assert.DoesNotContain('"' + name + '"', entry.Message);
        }

        Assert.Equal(0, service.ReadCount);
        Assert.Equal(0, body.ReadCount);
        Assert.Equal(0, body.DisposeCount);
        Assert.Single(context.HttpContext.Items);
        Assert.Same(marker, context.HttpContext.Items["existing"]);
    }

    /// <summary>
    /// Treats multipart diagnostics as metadata even when a bound DTO getter or request stream must never be touched.
    /// </summary>
    /// <param name="contentType">The native multipart media type, including a differently cased variation.</param>
    /// <returns>The omission, continuation and ownership assertions.</returns>
    [Theory]
    [InlineData("multipart/form-data; boundary=fixture")]
    [InlineData("MULTIPART/form-data; boundary=fixture")]
    public async Task MultipartRequestDoesNotReadBoundValuesOrTransport(string contentType)
    {
        using var fixture = new FilterFixture();
        using var body = new ProbeStream();
        var payload = new ThrowingGetterPayload();
        var context = CreateActionContext(new Dictionary<string, object?> { ["request"] = payload });
        context.HttpContext.Request.ContentType = contentType;
        context.HttpContext.Request.ContentLength = 2048;
        context.HttpContext.Request.Body = body;
        var invoked = false;

        await fixture.Filter.OnActionExecutionAsync(context, () =>
        {
            invoked = true;

            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });

        Assert.True(invoked);
        var entry = Assert.Single(fixture.Logs.Entries);
        Assert.Contains(ApiCommonConstants.Logging.MULTIPART, entry.Message);
        Assert.Contains("2048", entry.Message);
        Assert.Equal(0, payload.ReadCount);
        Assert.Equal(0, body.ReadCount);
        Assert.Equal(0, body.DisposeCount);
    }

    /// <summary>
    /// Rejects nested transport or deferred graphs using type metadata before evaluating their values.
    /// </summary>
    /// <param name="kind">The stream, uploaded file or deferred sequence fixture.</param>
    /// <returns>The safe omission and resource-isolation assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NestedUnsupportedGraphIsNotEvaluated(int kind)
    {
        using var fixture = new FilterFixture();
        using var stream = new ProbeStream();
        var deferred = new DeferredValues();
        object graph = kind switch
        {
            0 => new NestedPayload<NestedPayload<Stream>>(new NestedPayload<Stream>(stream)),
            1 => new NestedPayload<NestedPayload<IFormFile>>(
                new NestedPayload<IFormFile>(new FormFile(stream, 0, 0, "upload", "private-file.txt"))),
            _ => new NestedPayload<NestedPayload<IEnumerable<string>>>(new NestedPayload<IEnumerable<string>>(deferred))
        };
        var context = CreateActionContext(new Dictionary<string, object?> { ["request"] = graph });

        await fixture.Filter.OnActionExecutionAsync(context,
            () => Task.FromResult(new ActionExecutedContext(context, [], context.Controller)));

        Assert.Contains(ApiCommonConstants.Logging.UNSUPPORTED, Assert.Single(fixture.Logs.Entries).Message);
        Assert.Equal(0, deferred.EnumerationCount);
        Assert.Equal(0, stream.ReadCount);
        Assert.Equal(0, stream.DisposeCount);
    }

    /// <summary>
    /// Rejects an open declared DTO type whose runtime subtype adds deferred enumeration and a transport resource.
    /// </summary>
    /// <param name="resultStage">Whether the polymorphic graph is a result rather than an action argument.</param>
    /// <returns>The fail-closed omission, continuation and zero-consumption assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenNestedDtoCannotHideRuntimeTransportOrDeferredData(bool resultStage)
    {
        using var fixture = new FilterFixture();
        using var stream = new ProbeStream();
        var deferred = new DeferredValues();
        var payload = new PolymorphicPayload(new DeferredDerivedPayload(deferred, stream));
        var invoked = false;

        // The outer DTO is sealed, but its declared base property cannot prove the runtime graph safe without evaluation.
        if (resultStage)
        {
            var context = CreateResultContext(payload);
            await fixture.Filter.OnResultExecutionAsync(context, () =>
            {
                invoked = true;

                return Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller));
            });
        }
        else
        {
            var context = CreateActionContext(new Dictionary<string, object?> { ["request"] = payload });
            await fixture.Filter.OnActionExecutionAsync(context, () =>
            {
                invoked = true;

                return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
            });
        }

        Assert.True(invoked);
        Assert.Contains(ApiCommonConstants.Logging.UNSUPPORTED, Assert.Single(fixture.Logs.Entries).Message);
        Assert.Equal(0, deferred.EnumerationCount);
        Assert.Equal(0, stream.ReadCount);
        Assert.Equal(0, stream.DisposeCount);
    }

    /// <summary>
    /// Inspects public JSON fields as well as properties before allowing diagnostic serialization.
    /// </summary>
    /// <param name="resultStage">Whether the field-based DTO is returned rather than supplied to the action.</param>
    /// <returns>The metadata-only omission and absence of field enumeration.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicDeferredFieldCannotBypassGraphValidation(bool resultStage)
    {
        using var fixture = new FilterFixture();
        var deferred = new DeferredValues();
        var payload = new PublicFieldPayload(deferred);
        var invoked = false;

        // Newtonsoft includes public fields; ignoring them in a pre-serialization guard would still execute this sequence.
        if (resultStage)
        {
            var context = CreateResultContext(payload);
            await fixture.Filter.OnResultExecutionAsync(context, () =>
            {
                invoked = true;

                return Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller));
            });
        }
        else
        {
            var context = CreateActionContext(new Dictionary<string, object?> { ["request"] = payload });
            await fixture.Filter.OnActionExecutionAsync(context, () =>
            {
                invoked = true;

                return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
            });
        }

        Assert.True(invoked);
        Assert.Contains(ApiCommonConstants.Logging.UNSUPPORTED, Assert.Single(fixture.Logs.Entries).Message);
        Assert.Equal(0, deferred.EnumerationCount);
    }

    /// <summary>
    /// Never enumerates a streaming result merely to produce a second diagnostic representation.
    /// </summary>
    /// <param name="asynchronous">Whether the result uses the asynchronous enumeration contract.</param>
    /// <returns>The metadata-only result assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamingResultRemainsUnconsumed(bool asynchronous)
    {
        using var fixture = new FilterFixture();
        var deferred = new DeferredValues();
        var asyncDeferred = new DeferredAsyncValues();
        object payload = asynchronous ? asyncDeferred : deferred;
        var context = CreateResultContext(payload);

        // The continuation represents native result execution; this test isolates any extra enumeration by logging.
        await fixture.Filter.OnResultExecutionAsync(context,
            () => Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller)));

        Assert.Contains(ApiCommonConstants.Logging.UNSUPPORTED, Assert.Single(fixture.Logs.Entries).Message);
        Assert.Equal(0, deferred.EnumerationCount);
        Assert.Equal(0, asyncDeferred.EnumerationCount);
    }

    /// <summary>
    /// Falls back to safe metadata when the real Newtonsoft serializer encounters a DTO getter failure.
    /// </summary>
    /// <param name="resultStage">Whether failure occurs while recording a response rather than bound arguments.</param>
    /// <returns>The unchanged continuation and sanitized diagnostic assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSerializationFailureDoesNotChangeExecution(bool resultStage)
    {
        using var fixture = new FilterFixture();
        var payload = new ThrowingGetterPayload();
        var invoked = false;
        if (resultStage)
        {
            var context = CreateResultContext(payload);
            await fixture.Filter.OnResultExecutionAsync(context, () =>
            {
                invoked = true;

                return Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller));
            });
        }
        else
        {
            var context = CreateActionContext(new Dictionary<string, object?> { ["request"] = payload });
            await fixture.Filter.OnActionExecutionAsync(context, () =>
            {
                invoked = true;

                return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
            });
        }

        Assert.True(invoked);
        Assert.True(payload.ReadCount > 0);
        var warning = Assert.Single(fixture.Logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("JsonSerializationException", warning.Message);
        Assert.Null(warning.Exception);
        var diagnostic = Assert.Single(fixture.Logs.Entries, entry => entry.Level == LogLevel.Information);
        Assert.Contains(ApiCommonConstants.Logging.SERIALIZATION_FAILURE, diagnostic.Message);
        Assert.All(fixture.Logs.Entries, entry => Assert.DoesNotContain("private-getter-detail", entry.Message));
    }

    /// <summary>
    /// Applies the payload limit to the redacted copy, allowing a large credential that becomes a short mask.
    /// </summary>
    /// <param name="largeSafeValue">Whether the remaining safe data itself exceeds the limit.</param>
    /// <returns>The post-masking size and unmodified client-data assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PayloadLimitIsEvaluatedAfterMasking(bool largeSafeValue)
    {
        using var fixture = new FilterFixture();
        var oversized = new string('q', ApiCommonConstants.Logging.MAX_PAYLOAD_LENGTH + 256);
        var payload = largeSafeValue
            ? new DiagnosticPayload(oversized, "private-password")
            : new DiagnosticPayload("short-safe-value", oversized);
        var context = CreateResultContext(payload);

        await fixture.Filter.OnResultExecutionAsync(context,
            () => Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller)));

        var entry = Assert.Single(fixture.Logs.Entries);
        Assert.DoesNotContain(payload.Password, entry.Message);
        if (largeSafeValue)
        {
            Assert.Contains(ApiCommonConstants.Logging.SIZE_LIMIT, entry.Message);
            Assert.DoesNotContain(oversized, entry.Message);
        }
        else
        {
            Assert.Contains("short-safe-value", entry.Message);
            Assert.DoesNotContain(ApiCommonConstants.Logging.SIZE_LIMIT, entry.Message);
        }

        Assert.Same(payload, ((ObjectResult)context.Result).Value);
        Assert.Equal(oversized, largeSafeValue ? payload.Label : payload.Password);
    }

    /// <summary>
    /// Leaves business failures and cancellation from either MVC continuation to their existing owner.
    /// </summary>
    /// <param name="resultStage">Whether the result continuation fails.</param>
    /// <param name="cancellation">Whether the original failure represents cancellation.</param>
    /// <returns>The original exception identity and absence of successful response diagnostics.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ContinuationFailurePropagatesWithoutSuccessfulResponse(bool resultStage, bool cancellation)
    {
        using var fixture = new FilterFixture();
        Exception failure = cancellation
            ? new OperationCanceledException("continuation-canceled")
            : new InvalidOperationException("continuation-failed");
        Task Invoke()
        {
            if (resultStage)
            {
                var result = CreateResultContext(new DiagnosticPayload("safe", "secret"));

                return fixture.Filter.OnResultExecutionAsync(result,
                    () => Task.FromException<ResultExecutedContext>(failure));
            }

            var action = CreateActionContext(new Dictionary<string, object?>());

            return fixture.Filter.OnActionExecutionAsync(action,
                () => Task.FromException<ActionExecutedContext>(failure));
        }

        var actual = await Record.ExceptionAsync(Invoke);

        Assert.Same(failure, actual);
        Assert.DoesNotContain(fixture.Logs.Entries, entry => entry.Message.Contains("API response produced", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Logs.Entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Equal(resultStage ? 0 : 1, fixture.Logs.Entries.Count);
    }

    /// <summary>
    /// Does not claim a response was produced when native result execution reports cancellation or an unhandled failure.
    /// </summary>
    /// <param name="canceled">Whether execution was canceled instead of returning an unhandled exception.</param>
    /// <returns>The absence of a misleading successful diagnostic.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsuccessfulResultDoesNotProduceResponseEvent(bool canceled)
    {
        using var fixture = new FilterFixture();
        var context = CreateResultContext(new DiagnosticPayload("must-not-log", "private-value"));
        var executed = new ResultExecutedContext(context, [], context.Result, context.Controller)
        {
            Canceled = canceled,
            Exception = canceled ? null : new InvalidOperationException("result-failed")
        };

        await fixture.Filter.OnResultExecutionAsync(context, () => Task.FromResult(executed));

        Assert.Empty(fixture.Logs.Entries);
    }

    /// <summary>
    /// Uses the final result and transport status returned by inner result filters, rather than the original action result.
    /// </summary>
    /// <returns>The final payload, status and original-data assertions.</returns>
    [Fact]
    public async Task ResponseDiagnosticUsesExecutedResultAndActualStatus()
    {
        using var fixture = new FilterFixture();
        var context = CreateResultContext(new DiagnosticPayload("original-result", "first-secret"));
        var final = new ObjectResult(new DiagnosticPayload("adapted-result", "second-secret"));
        context.HttpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        await fixture.Filter.OnResultExecutionAsync(context,
            () => Task.FromResult(new ResultExecutedContext(context, [], final, context.Controller)));

        var entry = Assert.Single(fixture.Logs.Entries);
        Assert.Contains("adapted-result", entry.Message);
        Assert.Contains("422", entry.Message);
        Assert.DoesNotContain("original-result", entry.Message);
        Assert.DoesNotContain("second-secret", entry.Message);
        Assert.Equal("second-secret", Assert.IsType<DiagnosticPayload>(final.Value).Password);
        Assert.Equal("original-result", Assert.IsType<DiagnosticPayload>(((ObjectResult)context.Result).Value).Label);
    }

    /// <summary>
    /// Keeps request and result data local under interleaved execution while sharing the registered serializer.
    /// </summary>
    /// <returns>The twenty independent request/result and item-ownership assertions.</returns>
    [Fact]
    public async Task ParallelRequestsDoNotMixPayloadsOrAddSnapshots()
    {
        using var fixture = new FilterFixture();
        var workers = Enumerable.Range(0, 20).Select(async index =>
        {
            var logger = new TestLogger<ApiLoggingFilter>();
            var filter = fixture.CreateFilter(logger);
            var label = $"worker-{index:D2}-only";
            var payload = new DiagnosticPayload(label, $"worker-secret-{index:D2}");
            var action = CreateActionContext(new Dictionary<string, object?> { ["request"] = payload });
            var marker = new object();
            action.HttpContext.Items["existing"] = marker;

            await filter.OnActionExecutionAsync(action, async () =>
            {
                await Task.Yield();

                return new ActionExecutedContext(action, [], action.Controller);
            });

            var result = new ResultExecutingContext(action, [], new ObjectResult(payload), action.Controller);
            await filter.OnResultExecutionAsync(result, async () =>
            {
                await Task.Yield();

                return new ResultExecutedContext(result, [], result.Result, result.Controller);
            });

            Assert.Equal(2, logger.Entries.Count);
            Assert.All(logger.Entries, entry =>
            {
                Assert.Contains(label, entry.Message);
                Assert.DoesNotContain("worker-secret-", entry.Message);
                foreach (var other in Enumerable.Range(0, 20).Where(other => other != index))
                {
                    Assert.DoesNotContain($"worker-{other:D2}-only", entry.Message);
                }
            });
            Assert.Single(action.HttpContext.Items);
            Assert.Same(marker, action.HttpContext.Items["existing"]);
        });

        await Task.WhenAll(workers);
    }

    /// <summary>
    /// Renders actual filter-emitted events with the existing native text and compact-JSON sinks, including Both composition.
    /// </summary>
    /// <param name="mode">Zero selects compact JSON, one text, and two both output sinks.</param>
    /// <returns>The sink rendering, safe fields and pre-render redaction assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task FilterEventsRemainSafeInTextCompactJsonAndBoth(int mode)
    {
        using var fixture = new FilterFixture();
        var sink = new RecordingSink();
        using var serilog = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog, dispose: false);
        var filter = fixture.CreateFilter(factory.CreateLogger<ApiLoggingFilter>());
        var payload = new DiagnosticPayload("formatter-safe-value", "formatter-private-password");
        var action = CreateActionContext(new Dictionary<string, object?> { ["request"] = payload });
        await filter.OnActionExecutionAsync(action,
            () => Task.FromResult(new ActionExecutedContext(action, [], action.Controller)));
        var result = new ResultExecutingContext(action, [], new ObjectResult(payload), action.Controller);
        await filter.OnResultExecutionAsync(result,
            () => Task.FromResult(new ResultExecutedContext(result, [], result.Result, result.Controller)));

        Assert.Equal(2, sink.Events.Count);
        Assert.All(sink.Events, entry =>
        {
            var propertyName = entry.Properties.ContainsKey("RequestPayload") ? "RequestPayload" : "ResponsePayload";
            var redacted = Assert.IsType<ScalarValue>(entry.Properties[propertyName]).Value as string;
            Assert.NotNull(redacted);
            Assert.DoesNotContain("formatter-private-password", redacted);
        });

        bool[] formats = mode switch { 0 => [false], 1 => [true], _ => [true, false] };
        foreach (var useText in formats)
        {
            using var output = new StringWriter();
            var formatter = new MaskedConsoleFormatter(useText);
            foreach (var entry in sink.Events)
            {
                formatter.Format(entry, output);
            }

            var rendered = output.ToString();
            Assert.Contains("formatter-safe-value", rendered);
            Assert.DoesNotContain("formatter-private-password", rendered);
            if (!useText)
            {
                var records = rendered.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(2, records.Length);
                foreach (var record in records)
                {
                    using var document = JsonDocument.Parse(record);
                    Assert.True(document.RootElement.TryGetProperty("@mt", out _));
                }
            }
        }

        Assert.Equal("formatter-private-password", payload.Password);
    }

    /// <summary>
    /// Keeps protocol, SSO, health, documentation and near-match routes outside DTO payload diagnostics.
    /// </summary>
    /// <param name="path">A route not owned by the selected API boundary.</param>
    /// <returns>The absent payload and untouched DTO getter assertions.</returns>
    [Theory]
    [InlineData("/connect/token")]
    [InlineData("/login")]
    [InlineData("/health/live")]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/mcp")]
    [InlineData("/api/logging/v10/echo")]
    public async Task UnownedRoutesNeverEvaluatePayload(string path)
    {
        using var fixture = new FilterFixture();
        var payload = new ThrowingGetterPayload();
        var action = CreateActionContext(new Dictionary<string, object?> { ["request"] = payload });
        action.HttpContext.Request.Path = path;
        await fixture.Filter.OnActionExecutionAsync(action,
            () => Task.FromResult(new ActionExecutedContext(action, [], action.Controller)));
        var result = new ResultExecutingContext(action, [], new ObjectResult(payload), action.Controller);
        await fixture.Filter.OnResultExecutionAsync(result,
            () => Task.FromResult(new ResultExecutedContext(result, [], result.Result, result.Controller)));

        Assert.Empty(fixture.Logs.Entries);
        Assert.Equal(0, payload.ReadCount);
    }

    /// <summary>
    /// Creates a native bound action context inside the host-selected diagnostic boundary.
    /// </summary>
    /// <param name="arguments">Already-bound action inputs.</param>
    /// <returns>The request-local MVC context.</returns>
    private static ActionExecutingContext CreateActionContext(IDictionary<string, object?> arguments)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/logging/v1/probe";
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(action, [], arguments, new object());
    }

    /// <summary>
    /// Creates a result context without executing or wrapping its transport payload.
    /// </summary>
    /// <param name="payload">The native ObjectResult value.</param>
    /// <returns>The request-local result context.</returns>
    private static ResultExecutingContext CreateResultContext(object payload)
    {
        var action = CreateActionContext(new Dictionary<string, object?>());

        return new ResultExecutingContext(action, [], new ObjectResult(payload), action.Controller);
    }

    /// <summary>
    /// Owns the real DI serializer while keeping logger state private to one test or worker.
    /// </summary>
    private sealed class FilterFixture : IDisposable
    {
        /// <summary>
        /// Registers only the existing serializer contract without a second serializer configuration.
        /// </summary>
        public FilterFixture()
        {
            Services = new ServiceCollection().AddJsonSerializer().BuildServiceProvider();
            Filter = CreateFilter(Logs);
        }

        /// <summary>
        /// Gets the test-owned provider supplying the production serializer.
        /// </summary>
        public ServiceProvider Services { get; }

        /// <summary>
        /// Gets the ordinary pre-formatter capture logger.
        /// </summary>
        public TestLogger<ApiLoggingFilter> Logs { get; } = new();

        /// <summary>
        /// Gets the filter under test.
        /// </summary>
        public ApiLoggingFilter Filter { get; }

        /// <summary>
        /// Shares the DI serializer without sharing request or logging state.
        /// </summary>
        /// <param name="logger">The independently owned diagnostic destination.</param>
        /// <returns>The production filter instance.</returns>
        public ApiLoggingFilter CreateFilter(ILogger<ApiLoggingFilter> logger) => new(
            new ApiResponseBoundary("/api/logging/v1", "9.2"),
            Services.GetRequiredService<IJsonSerializerService>(), logger);

        /// <summary>
        /// Releases only the provider created by this test fixture.
        /// </summary>
        public void Dispose() => Services.Dispose();
    }

    /// <summary>
    /// Supplies safe data and one credential through the serializer's ordinary property contract.
    /// </summary>
    /// <param name="Label">The ordinary value that should remain visible.</param>
    /// <param name="Password">The credential that must be masked before logging.</param>
    private sealed record DiagnosticPayload(string Label, string Password);

    /// <summary>
    /// Builds a typed nested graph without hiding its declared transport or enumeration contract.
    /// </summary>
    /// <typeparam name="TValue">The nested payload type.</typeparam>
    /// <param name="Value">The nested value.</param>
    private sealed record NestedPayload<TValue>(TValue Value);

    /// <summary>
    /// Declares an otherwise ordinary DTO contract that can legally gain additional runtime behavior.
    /// </summary>
    private class BasePayload
    {
        /// <summary>
        /// Gets the safe value visible through the declared base contract.
        /// </summary>
        public string Label => "base-safe-value";
    }

    /// <summary>
    /// Seals the outer contract while intentionally retaining an open nested DTO property.
    /// </summary>
    /// <param name="Value">The declared base DTO whose runtime subtype is not statically bounded.</param>
    private sealed record PolymorphicPayload(BasePayload Value);

    /// <summary>
    /// Adds a deferred contract and stream that must not be discovered by serializing the base-typed value.
    /// </summary>
    /// <param name="values">The sequence tracking unexpected enumeration.</param>
    /// <param name="attachment">The transport resource whose ownership belongs to the test.</param>
    private sealed class DeferredDerivedPayload(DeferredValues values, Stream attachment) : BasePayload, IEnumerable<string>
    {
        /// <summary>
        /// Gets a transport property absent from the declared base contract.
        /// </summary>
        public Stream Attachment { get; } = attachment;

        /// <summary>
        /// Delegates to the existing consumption guard if diagnostics attempts runtime enumeration.
        /// </summary>
        /// <returns>The guarded sequence enumerator.</returns>
        public IEnumerator<string> GetEnumerator() => values.GetEnumerator();

        /// <summary>
        /// Keeps the non-generic native contract subject to the same enumeration guard.
        /// </summary>
        /// <returns>The guarded sequence enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Exposes an ordinary public JSON field whose deferred contract must be inspected before serialization.
    /// </summary>
    /// <param name="values">The sequence that records accidental enumeration.</param>
    private sealed class PublicFieldPayload(IEnumerable<string> values)
    {
        /// <summary>
        /// Supplies a public field handled by Newtonsoft's default object contract.
        /// </summary>
        public readonly IEnumerable<string> ITEMS = values;
    }

    /// <summary>
    /// Exercises native Newtonsoft getter failure without substituting a mock serializer.
    /// </summary>
    private sealed class ThrowingGetterPayload
    {
        /// <summary>
        /// Gets how many times actual serialization attempted the protected getter.
        /// </summary>
        public int ReadCount { get; private set; }

        /// <summary>
        /// Fails only when evaluated, allowing metadata inspection to remain harmless.
        /// </summary>
        public string Value
        {
            get
            {
                ReadCount++;

                throw new InvalidOperationException("private-getter-detail");
            }
        }
    }

    /// <summary>
    /// Detects any unexpected evaluation of a deferred enumerable by diagnostics.
    /// </summary>
    private sealed class DeferredValues : IEnumerable<string>
    {
        /// <summary>
        /// Gets the number of requested enumerators.
        /// </summary>
        public int EnumerationCount { get; private set; }

        /// <summary>
        /// Fails if logging attempts to consume a deferred sequence.
        /// </summary>
        /// <returns>No enumerator, because evaluation is forbidden in this fixture.</returns>
        public IEnumerator<string> GetEnumerator()
        {
            EnumerationCount++;

            throw new InvalidOperationException("Deferred sequence was consumed by diagnostics.");
        }

        /// <summary>
        /// Routes the native non-generic contract through the same consumption guard.
        /// </summary>
        /// <returns>The guarded enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Detects enumeration of an asynchronous response stream without allocating or scheduling its items.
    /// </summary>
    private sealed class DeferredAsyncValues : IAsyncEnumerable<string>
    {
        /// <summary>
        /// Gets the number of requested asynchronous enumerators.
        /// </summary>
        public int EnumerationCount { get; private set; }

        /// <summary>
        /// Fails if diagnostics tries to consume the native streaming result.
        /// </summary>
        /// <param name="cancellationToken">The native enumeration cancellation token.</param>
        /// <returns>No enumerator, because evaluation is forbidden in this fixture.</returns>
        public IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            EnumerationCount++;

            throw new InvalidOperationException("Async sequence was consumed by diagnostics.");
        }
    }

    /// <summary>
    /// Detects transport ownership violations while remaining a valid native stream for request/file fixtures.
    /// </summary>
    private sealed class ProbeStream : MemoryStream
    {
        /// <summary>
        /// Gets the number of attempted reads.
        /// </summary>
        public int ReadCount { get; private set; }

        /// <summary>
        /// Gets the number of disposal attempts.
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <summary>
        /// Rejects synchronous transport reads by the diagnostic layer.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">The buffer offset.</param>
        /// <param name="count">The requested byte count.</param>
        /// <returns>No byte count, because transport reads are forbidden in this fixture.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;

            throw new InvalidOperationException("Transport body was read by diagnostics.");
        }

        /// <summary>
        /// Rejects asynchronous transport reads by the diagnostic layer.
        /// </summary>
        /// <param name="buffer">The destination memory.</param>
        /// <param name="cancellationToken">The native read cancellation token.</param>
        /// <returns>No byte count, because transport reads are forbidden in this fixture.</returns>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCount++;

            throw new InvalidOperationException("Transport body was read asynchronously by diagnostics.");
        }

        /// <summary>
        /// Records release attempts so tests can distinguish logging from the resource owner's cleanup.
        /// </summary>
        /// <param name="disposing">Whether managed state is being released.</param>
        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Retains real Serilog events locally without redirecting Console or destructuring DTOs itself.
    /// </summary>
    private sealed class RecordingSink : ILogEventSink
    {
        /// <summary>
        /// Gets events produced synchronously by the native Microsoft logging bridge.
        /// </summary>
        public List<LogEvent> Events { get; } = [];

        /// <summary>
        /// Captures a completed native event for pre-render and formatter assertions.
        /// </summary>
        /// <param name="logEvent">The completed structured event.</param>
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
