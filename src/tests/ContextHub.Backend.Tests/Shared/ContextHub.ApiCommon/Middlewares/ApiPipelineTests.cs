namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Middlewares;

/// <summary>
/// Verifies Admin response envelopes, correlation metadata and safe failure handling using an in-memory HTTP host.
/// </summary>
public sealed class ApiPipelineTests
{
    /// <summary>
    /// Translates the neutral missing-authentication failure to the safe Admin envelope, not forbidden or server error.
    /// </summary>
    /// <returns>The asynchronous HTTP assertions.</returns>
    [Fact]
    public async Task MissingAuthentication_UsesUnauthorizedEnvelope()
    {
        await using var server = await CreateServer(app =>
        {
            app.UseExceptionHandler();
            app.Run(_ => throw new CoreAuthenticationException());
        });
        using var client = server.GetTestClient();
        using var response = await client.GetAsync("/api/admin/v1/example");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("error_unauthorized", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        AssertMetadata(response, json.RootElement);
    }

    /// <summary>
    /// Verifies that a denied application request returns forbidden rather than an unexpected server error.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task AuthorizationDenied_UsesForbiddenEnvelope()
    {
        await using var server = await CreateServer(app =>
        {
            app.UseExceptionHandler();
            app.Run(_ => throw new CoreAuthorizationException());
        });
        using var client = server.GetTestClient();
        using var response = await client.GetAsync("/api/admin/v1/example");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("error_forbidden", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        AssertMetadata(response, json.RootElement);
    }

    /// <summary>
    /// Verifies that correlation phase logs omit the rejected raw header value.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task CorrelationLogs_DoNotIncludeRejectedHeaderValues()
    {
        var logger = new TestLogger<CorrelationMiddleware>();
        await using var server = await CreateServer(
            app => app.Run(_ => Task.CompletedTask),
            services => services.AddSingleton<ILogger<CorrelationMiddleware>>(logger));
        using var client = server.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/v1/example");
        request.Headers.TryAddWithoutValidation(ApiCommonConstants.CORRELATION_ID, "private-invalid-header");
        using var response = await client.SendAsync(request);

        Assert.Contains(
            logger.Entries,
            entry => entry.Message.Contains("initialized", StringComparison.Ordinal));
        Assert.Contains(
            logger.Entries,
            entry => entry.Message.Contains("pipeline returned", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.DoesNotContain(
            "private-invalid-header",
            string.Join('\n', logger.Entries.Select(entry => entry.Message)));
    }

    /// <summary>
    /// Verifies that empty Admin failures preserve HTTP status and protocol headers while producing the safe envelope.
    /// </summary>
    /// <param name="status">The HTTP status selected for the scenario.</param>
    /// <param name="code">The safe public error code supplied by the fixture.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData(400, "error_validation")]
    [InlineData(401, "error_unauthorized")]
    [InlineData(403, "error_forbidden")]
    [InlineData(404, "error_not_found")]
    [InlineData(405, "error_method_not_allowed")]
    [InlineData(406, "error_not_acceptable")]
    [InlineData(413, "error_payload_too_large")]
    [InlineData(415, "error_unsupported_media_type")]
    [InlineData(429, "error_rate_limited")]
    public async Task EmptyApiErrors_PreserveStatusHeadersAndSafeEnvelope(int status, string code)
    {
        await using var server = await CreateServer(app => app.Run(context =>
        {
            context.Response.StatusCode = status;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            context.Response.Headers.Allow = "POST";
            context.Response.Headers.RetryAfter = "5";
            return Task.CompletedTask;
        }));
        using var client = server.GetTestClient();
        using var response = await client.GetAsync("/api/admin/v1/example");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(status, (int)response.StatusCode);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(code, json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(json.RootElement.GetProperty("error").GetProperty("details").EnumerateArray());
        Assert.Equal("5", response.Headers.GetValues("Retry-After").Single());
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Equal("POST", response.Content.Headers.Allow.Single());
        AssertMetadata(response, json.RootElement);
    }

    /// <summary>
    /// Verifies that MCP, health and OIDC responses are not rewritten as Admin envelopes.
    /// </summary>
    /// <param name="path">The request path or configured path exercised by this scenario.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("/mcp")]
    [InlineData("/health/ready")]
    [InlineData("/connect/token")]
    [InlineData("/api/admin/v10/example")]
    public async Task NativeProtocols_AreNotWrapped(string path)
    {
        await using var server = await CreateServer(app => app.Run(context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        }));
        using var client = server.GetTestClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Verifies that canonical correlation IDs are preserved and invalid inputs are replaced without disclosure.
    /// </summary>
    /// <param name="incoming">The incoming correlation identifier submitted by the fixture.</param>
    /// <param name="preserved">Whether the incoming correlation identifier must be retained.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("7f620268-624b-4208-80ec-d284e6217d33", true)]
    [InlineData("invalid-secret-value", false)]
    [InlineData("", false)]
    [InlineData("7f620268624b420880ecd284e6217d33", false)]
    public async Task Correlation_PreservesCanonicalUuidAndReplacesInvalidInput(string incoming, bool preserved)
    {
        await using var server = await CreateServer(app => app.Run(context =>
            context.Response.WriteAsJsonAsync(context.RequestServices.GetRequiredService<ApiErrorResponseFactory>().Create(context, 404))));
        using var client = server.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/v1/example");
        request.Headers.TryAddWithoutValidation(ApiCommonConstants.CORRELATION_ID, incoming);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var actual = response.Headers.GetValues(ApiCommonConstants.CORRELATION_ID).Single();

        Assert.True(Guid.TryParseExact(actual, "D", out _));
        Assert.Equal(preserved, incoming == actual);
        if (incoming == "invalid-secret-value") Assert.DoesNotContain(incoming, body);
        AssertMetadata(response, json.RootElement);
    }

    /// <summary>
    /// Verifies that automatic MVC binding failures use the Admin envelope without echoing submitted values.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task AutomaticModelValidation_UsesEnvelopeWithoutEchoingBindingValue()
    {
        await using var server = await CreateServer(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
        using var client = server.GetTestClient();
        using var response = await client.PostAsync("/api/admin/v1/test-validation",
            new StringContent("{\"count\":\"secret-binding-value\"}", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "error_validation",
            json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.NotEmpty(json.RootElement.GetProperty("error").GetProperty("details").EnumerateArray());
        Assert.DoesNotContain("secret-binding-value", body);
        Assert.DoesNotContain("ProblemDetails", body);
        AssertMetadata(response, json.RootElement);
    }

    /// <summary>
    /// Keeps controller-generated failures in the Admin envelope instead of MVC's default problem response.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ControllerNotFound_UsesAdminEnvelope()
    {
        await using var server = await CreateServer(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
        using var client = server.GetTestClient();
        using var response = await client.GetAsync("/api/admin/v1/test-validation/missing");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("error_not_found", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        AssertMetadata(response, json.RootElement);
    }

    /// <summary>
    /// Preserves the native MVC error contract on a non-Admin controller route.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ControllerNotFound_OutsideAdmin_PreservesNativeProblemResponse()
    {
        await using var server = await CreateServer(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
        using var client = server.GetTestClient();
        using var response = await client.GetAsync("/test-native/missing");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("success", out _));
    }

    /// <summary>
    /// Adapts MVC's unsupported input formatter result even when the controller action never executes.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task UnsupportedInputMediaType_UsesAdminEnvelope()
    {
        await using var server = await CreateServer(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
        using var client = server.GetTestClient();
        using var response = await client.PostAsync("/api/admin/v1/test-validation",
            new StringContent("private-input", Encoding.UTF8, "text/plain"));
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("error_unsupported_media_type", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("private-input", body, StringComparison.Ordinal);
        AssertMetadata(response, json.RootElement);
    }

    /// <summary>
    /// Verifies that an unexpected exception produces a safe server error without exposing its original message.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task UnhandledException_DoesNotLeakOriginalMessage()
    {
        await using var server = await CreateServer(app =>
        {
            app.UseExceptionHandler();
            app.Run(_ => throw new InvalidOperationException("password=secret-source-value"));
        });
        using var client = server.GetTestClient();
        using var response = await client.GetAsync("/api/admin/v1/example");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("error_internal", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("secret-source-value", body);
        AssertMetadata(response, json.RootElement);
    }

    /// <summary>
    /// Verifies that error responses discard data and do not duplicate HTTP status in the JSON body.
    /// </summary>
    [Fact]
    public void ErrorEnvelope_NeverRetainsDataOrPublishesStatusCode()
    {
        var result = new ApiResponse<object>(new { privateValue = "hidden" }, new ApiError("error_conflict", "Conflict.", []));
        var json = JsonSerializer.Serialize(result);

        Assert.Null(result.Data);
        Assert.False(result.Success);
        Assert.DoesNotContain("privateValue", json);
        Assert.DoesNotContain("statusCode", json);
        Assert.Contains("\"details\":[]", json);
    }

    /// <summary>
    /// Starts an isolated HTTP test host with shared Admin middleware and optional test-specific service overrides.
    /// </summary>
    /// <param name="configure">The callback configuring the registration under test.</param>
    /// <param name="configureServices">The callback composing this test host's service collection.</param>
    /// <returns>The started test application; the caller owns its shutdown and disposal.</returns>
    private static async Task<WebApplication> CreateServer(
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ValidationProbeController).Assembly);
        builder.Services.AddApiInfrastructure("/api/admin/v1", "1.0");
        builder.Services.AddProblemDetails();
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseCorrelation();
        app.UseApiStatusCodes();
        configure(app);
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Verifies correlation consistency, API version, trace identifiers and timestamp ordering.
    /// </summary>
    /// <param name="response">The response metadata being modified by this test fixture.</param>
    /// <param name="root">The parsed document root inspected by the assertion helper.</param>
    private static void AssertMetadata(HttpResponseMessage response, JsonElement root)
    {
        var meta = root.GetProperty("meta");
        Assert.Equal(
            response.Headers.GetValues(ApiCommonConstants.CORRELATION_ID).Single(),
            meta.GetProperty("correlationId").GetString());
        Assert.Equal("1.0", meta.GetProperty("version").GetString());
        Assert.Equal(32, meta.GetProperty("traceId").GetString()!.Length);
        Assert.Equal(16, meta.GetProperty("spanId").GetString()!.Length);
        Assert.True(meta.GetProperty("responseTimestamp").GetDateTimeOffset() >= meta.GetProperty("requestTimestamp").GetDateTimeOffset());
    }
}

/// <summary>
/// Provides a test-only Admin endpoint for automatic MVC model validation.
/// </summary>
[ApiController]
[Route("api/admin/v1/test-validation")]
public sealed class ValidationProbeController : ControllerBase
{
    /// <summary>
    /// Produces a controller failure that the native client-error filter would normally map to ProblemDetails.
    /// </summary>
    /// <returns>The synthetic action result exercised by the real HTTP pipeline.</returns>
    [HttpGet("missing")]
    public IActionResult Missing() => NotFound();

    /// <summary>
    /// Returns the bound request so tests can distinguish successful binding from automatic validation failures.
    /// </summary>
    /// <param name="request">The request body or integration DTO supplied by the caller.</param>
    /// <returns>The response envelope containing the submitted fixture request.</returns>
    [HttpPost]
    public ApiResponse<ValidationProbeRequest> Post(ValidationProbeRequest request) => new(request);
}

/// <summary>
/// Exercises the native MVC contract outside the Admin transport boundary.
/// </summary>
[ApiController]
[Route("test-native")]
public sealed class NativeProbeController : ControllerBase
{
    /// <summary>
    /// Returns a native controller error for boundary-preservation assertions.
    /// </summary>
    /// <returns>The synthetic action result exercised by the real HTTP pipeline.</returns>
    [HttpGet("missing")]
    public IActionResult Missing() => NotFound();
}

/// <summary>
/// Defines the constrained input used to exercise MVC binding and validation errors.
/// </summary>
public sealed class ValidationProbeRequest
{
    /// <summary>
    /// Gets the test input constrained to values from one through ten.
    /// </summary>
    [Range(1, 10)]
    public int Count { get; init; }
}
