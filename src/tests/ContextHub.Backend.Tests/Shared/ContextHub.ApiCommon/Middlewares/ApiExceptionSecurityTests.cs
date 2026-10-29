namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Middlewares;

/// <summary>
/// Verifies that global exception responses classify failures without publishing diagnostic details.
/// </summary>
public sealed class ApiExceptionSecurityTests
{
    /// <summary>
    /// Translates neutral categories while withholding the feature's custom code and diagnostic description.
    /// </summary>
    /// <param name="kind">The transport-neutral business classification.</param>
    /// <param name="status">The expected HTTP adapter status.</param>
    /// <param name="code">The expected generic public error code.</param>
    /// <returns>The asynchronous classification and privacy assertions.</returns>
    [Theory]
    [InlineData(BusinessErrorKind.Invalid, 400, "error_validation")]
    [InlineData(BusinessErrorKind.NotFound, 404, "error_not_found")]
    [InlineData(BusinessErrorKind.Conflict, 409, "error_conflict")]
    public async Task BusinessFailure_UsesGenericStatusContract(BusinessErrorKind kind, int status, string code)
    {
        await using var app = await CreateServer(new BusinessException("private_feature_code", "business-private-sentinel", kind));
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/shared/v1/example");

        await AssertError(response, status, code);
    }

    /// <summary>
    /// Retains an explicitly supported API status without publishing custom exception text or identifiers.
    /// </summary>
    /// <param name="status">The declared HTTP failure.</param>
    /// <param name="code">The expected generic public code.</param>
    /// <returns>The asynchronous generic API response assertions.</returns>
    [Theory]
    [InlineData(400, "error_validation")]
    [InlineData(401, "error_unauthorized")]
    [InlineData(403, "error_forbidden")]
    [InlineData(404, "error_not_found")]
    [InlineData(405, "error_method_not_allowed")]
    [InlineData(406, "error_not_acceptable")]
    [InlineData(409, "error_conflict")]
    [InlineData(413, "error_payload_too_large")]
    [InlineData(415, "error_unsupported_media_type")]
    [InlineData(429, "error_rate_limited")]
    [InlineData(500, "error_internal")]
    [InlineData(503, "error_dependency_unavailable")]
    public async Task ApiFailure_DoesNotPublishCustomExceptionValues(int status, string code)
    {
        await using var app = await CreateServer(new ApiException(status, "private_api_code", "api-private-sentinel"));
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/shared/v1/example");

        await AssertError(response, status, code);
    }

    /// <summary>
    /// Does not reinterpret programmer errors or upstream status codes as incoming business failures.
    /// </summary>
    /// <param name="scenario">The controlled native exception category.</param>
    /// <returns>The asynchronous safe server response assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task UnexpectedFailure_IsNotGuessedFromNativeTypeOrUpstreamStatus(int scenario)
    {
        Exception failure = scenario switch
        {
            0 => new ArgumentException("argument-private-sentinel"),
            1 => new KeyNotFoundException("lookup-private-sentinel"),
            _ => new HttpRequestException("upstream-private-sentinel", null, HttpStatusCode.NotFound)
        };
        await using var app = await CreateServer(failure);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/shared/v1/example");

        await AssertError(response, 500, "error_internal");
    }

    /// <summary>
    /// Preserves supported native request statuses without exposing the framework's binding message.
    /// </summary>
    /// <param name="status">The transport failure selected by the framework.</param>
    /// <param name="code">The independently specified public error code.</param>
    /// <returns>The asynchronous HTTP response assertions.</returns>
    [Theory]
    [InlineData(400, "error_validation")]
    [InlineData(413, "error_payload_too_large")]
    [InlineData(415, "error_unsupported_media_type")]
    public async Task NativeRequestFailure_PreservesStatusWithoutDetails(int status, string code)
    {
        await using var app = await CreateServer(new BadHttpRequestException("binding-private-sentinel", status));
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/shared/v1/example");

        await AssertError(response, status, code);
    }

    /// <summary>
    /// Distinguishes a known coordination outage from an unexpected failure without exposing its inner exception.
    /// </summary>
    /// <returns>The asynchronous safe dependency response assertions.</returns>
    [Fact]
    public async Task LockUnavailable_UsesServiceUnavailableWithoutProviderDetails()
    {
        var failure = new DistributedLockUnavailableException(new InvalidOperationException("provider-private-sentinel"));
        await using var app = await CreateServer(failure);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/shared/v1/example");

        await AssertError(response, 503, "error_dependency_unavailable");
    }

    /// <summary>
    /// Keeps field diagnostics internal when validation arrives through the global exception boundary.
    /// </summary>
    /// <returns>The asynchronous generic validation response assertions.</returns>
    [Fact]
    public async Task ValidationException_DoesNotPublishFieldDiagnostics()
    {
        var failure = new CoreValidationException([new ValidationError("privateField", "private_code", "validation-private-sentinel")]);
        await using var app = await CreateServer(failure);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/shared/v1/example");

        await AssertError(response, 400, "error_validation");
    }

    /// <summary>
    /// Keeps nested exceptions and arbitrary diagnostic data out of both the response and the handler's log payload.
    /// </summary>
    /// <returns>The asynchronous response and metadata-only logging assertions.</returns>
    [Fact]
    public async Task UnexpectedFailure_DoesNotPublishInnerExceptionOrData()
    {
        var failure = new InvalidOperationException("outer-private-sentinel", new Exception("inner-private-sentinel"));
        failure.Data["private_field"] = "data-private-sentinel";
        var logger = new TestLogger<ApiExceptionHandler>();
        await using var app = await CreateServer(failure, logger);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/shared/v1/example");

        await AssertError(response, 500, "error_internal");
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Contains(nameof(InvalidOperationException), entry.Message);
        Assert.DoesNotContain("private", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Leaves protocol ownership and disconnected requests untouched before any replacement body is constructed.
    /// </summary>
    /// <param name="scenario">The boundary, already-started or disconnected-request guard exercised by the fixture.</param>
    /// <returns>The asynchronous handler ownership assertions.</returns>
    [Theory]
    [InlineData("outside")]
    [InlineData("started")]
    [InlineData("cancelled")]
    public async Task BypassedFailure_DoesNotWriteAnotherResponse(string scenario)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiInfrastructure("/api/shared/v1", "2.0");
        await using var provider = services.BuildServiceProvider();
        using var body = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Path = scenario == "outside" ? "/connect/token" : "/api/shared/v1/example";
        http.Response.Body = body;

        if (scenario == "started")
        {
            http.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        }

        if (scenario == "cancelled")
        {
            await cancellation.CancelAsync();
            http.RequestAborted = cancellation.Token;
        }

        // Deliberately omit correlation metadata: bypassed failures must not attempt to build an error envelope.
        Exception failure = scenario == "cancelled" ? new OperationCanceledException(cancellation.Token)
            : new InvalidOperationException("guard-private-sentinel");
        var handler = Assert.Single(provider.GetServices<IExceptionHandler>());
        var handled = await handler.TryHandleAsync(http, failure, cancellation.Token);

        Assert.False(handled);
        Assert.Equal(0, body.Length);
    }

    /// <summary>
    /// Represents headers already sent by the server without creating a second transport implementation.
    /// </summary>
    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        /// <summary>
        /// Gets the fixture's already-started response state.
        /// </summary>
        public override bool HasStarted => true;
    }

    /// <summary>
    /// Starts a real HTTP pipeline using the registered correlation and exception adapters.
    /// </summary>
    /// <param name="failure">The controlled fixture failure, never a real provider exception.</param>
    /// <param name="logger">An optional capture of the real handler's metadata-only diagnostic event.</param>
    /// <returns>The started application owned by the calling test.</returns>
    private static async Task<WebApplication> CreateServer(Exception failure, TestLogger<ApiExceptionHandler>? logger = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiInfrastructure("/api/shared/v1", "2.0");

        if (logger is not null)
        {
            builder.Services.AddSingleton<ILogger<ApiExceptionHandler>>(logger);
        }

        var app = builder.Build();
        app.UseCorrelation();
        app.UseApiErrorHandling();
        app.Run(_ => throw failure);
        await app.StartAsync();

        return app;
    }

    /// <summary>
    /// Checks the standard wire shape and metadata without deriving expectations from the production mapper.
    /// </summary>
    /// <param name="response">The actual HTTP response.</param>
    /// <param name="status">The expected contract status.</param>
    /// <param name="code">The expected generic public code.</param>
    /// <returns>The asynchronous body and metadata assertions.</returns>
    private static async Task AssertError(HttpResponseMessage response, int status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var error = root.GetProperty("error");

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);
        Assert.Empty(error.GetProperty("details").EnumerateArray());
        Assert.DoesNotContain("private", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.Equal("2.0", root.GetProperty("meta").GetProperty("version").GetString());
        Assert.Equal(response.Headers.GetValues(ApiCommonConstants.CORRELATION_ID).Single(),
            root.GetProperty("meta").GetProperty("correlationId").GetString());
    }
}
