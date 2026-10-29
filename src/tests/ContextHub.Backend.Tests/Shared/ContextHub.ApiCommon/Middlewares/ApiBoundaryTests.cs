namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Middlewares;

/// <summary>
/// Exercises the host-selected response boundary through real HTTP responses rather than shared constants.
/// </summary>
public sealed class ApiBoundaryTests
{
    /// <summary>
    /// Rejects a second boundary before it can mix captured MVC options with a different DI-owned boundary.
    /// </summary>
    /// <param name="prefix">The prefix supplied by the second registration.</param>
    /// <param name="version">The version supplied by the second registration.</param>
    [Theory]
    [InlineData("/api/reports/v2", "2.5")]
    [InlineData("/api/catalog/v3", "3.1")]
    public void DuplicateBoundary_IsRejectedWithoutChangingRegistrations(string prefix, string version)
    {
        var services = new ServiceCollection();
        services.AddApiInfrastructure("/api/reports/v2", "2.5");
        var originalDescriptors = services.ToArray();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddApiInfrastructure(prefix, version));

        Assert.Equal(ApiCommonErrorConstants.BoundaryErrors.ALREADY_REGISTERED, exception.Message);
        Assert.Equal(originalDescriptors, services.ToArray());
    }

    /// <summary>
    /// Keeps separate host prefixes and response versions isolated while leaving near-match paths untouched.
    /// </summary>
    /// <returns>The asynchronous assertions against both independently composed hosts.</returns>
    [Fact]
    public async Task Hosts_UseTheirOwnPrefixAndVersion()
    {
        await using var first = await CreateHost("/api/reports/v2", "2.5");
        await using var second = await CreateHost("/api/catalog/v3", "3.1");
        using var firstClient = first.GetTestClient();
        using var secondClient = second.GetTestClient();

        using var firstResponse = await firstClient.GetAsync("/api/reports/v2/missing");
        using var secondResponse = await secondClient.GetAsync("/api/catalog/v3/missing");
        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());

        Assert.Equal("2.5", firstJson.RootElement.GetProperty("meta").GetProperty("version").GetString());
        Assert.Equal("3.1", secondJson.RootElement.GetProperty("meta").GetProperty("version").GetString());

        // A similar prefix and the other host's route are not part of this host's declared API boundary.
        foreach (var path in new[] { "/api/reports/v20/missing", "/api/catalog/v3/missing", "/mcp", "/health/ready", "/connect/token" })
        {
            using var response = await firstClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// Rejects ambiguous route configuration before any response middleware can be installed.
    /// </summary>
    /// <param name="prefix">The invalid prefix supplied by a host.</param>
    [Theory]
    [InlineData("/")]
    [InlineData("")]
    [InlineData("api/reports")]
    [InlineData("/api//reports")]
    [InlineData("/api/reports/")]
    [InlineData("/api/reports?x=1")]
    [InlineData("/api/reports#fragment")]
    [InlineData("/api/ reports")]
    public void InvalidPrefix_IsRejectedAtRegistration(string prefix) =>
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddApiInfrastructure(prefix, "1.0"));

    /// <summary>
    /// Rejects a missing response version instead of using an implicit product version.
    /// </summary>
    [Fact]
    public void BlankVersion_IsRejectedAtRegistration() =>
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddApiInfrastructure("/api/reports", " "));

    /// <summary>
    /// Prevents documentation configuration from assuming an unregistered API response boundary.
    /// </summary>
    [Fact]
    public void OpenApi_RequiresExplicitBoundary()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddOpenApiInfrastructure(_ => { }));

        Assert.Equal(ApiCommonErrorConstants.BoundaryErrors.NOT_REGISTERED, exception.Message);
    }

    /// <summary>
    /// Uses the same boundary version in generated error examples and live HTTP response metadata.
    /// </summary>
    /// <returns>The document and runtime assertions.</returns>
    [Fact]
    public async Task OpenApi_UsesRuntimeBoundaryVersion()
    {
        await using var host = await CreateHost("/api/reports/v2", "2.5");
        using var client = host.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var example = paths.GetProperty("/api/reports/v2/protected").GetProperty("get")
            .GetProperty("responses").GetProperty("500").GetProperty("content").GetProperty("application/json")
            .GetProperty("example");

        Assert.Equal("2.5", example.GetProperty("meta").GetProperty("version").GetString());
        Assert.Equal("error_internal", example.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>
    /// Adapts native authentication challenge and authorization forbid, but preserves explicit result bodies.
    /// </summary>
    /// <returns>The authentication, authorization and result-ownership assertions.</returns>
    [Fact]
    public async Task AuthenticationPipeline_UsesSafeEnvelopeAndPreservesBodies()
    {
        await using var host = await CreateHost("/api/reports/v2", "2.5");
        using var client = host.GetTestClient();
        using var unauthorized = await client.GetAsync("/api/reports/v2/protected");
        using var unauthorizedJson = JsonDocument.Parse(await unauthorized.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("error_unauthorized", unauthorizedJson.RootElement.GetProperty("error").GetProperty("code").GetString());

        client.DefaultRequestHeaders.Add("X-Fixture-Authenticated", "true");
        using var forbidden = await client.GetAsync("/api/reports/v2/protected");
        using var forbiddenJson = JsonDocument.Parse(await forbidden.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("error_forbidden", forbiddenJson.RootElement.GetProperty("error").GetProperty("code").GetString());

        using var explicitBody = await client.GetAsync("/api/reports/v2/body");
        Assert.Equal(HttpStatusCode.NotFound, explicitBody.StatusCode);
        Assert.Equal("unchanged", await explicitBody.Content.ReadAsStringAsync());

        using var file = await client.GetAsync("/api/reports/v2/file");
        Assert.Equal("file-payload", await file.Content.ReadAsStringAsync());

        using var failure = await client.GetAsync("/api/reports/v2/failure");
        var safeBody = await failure.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.DoesNotContain("private-failure", safeBody);
    }

    /// <summary>
    /// Leaves a native MVC result untouched after response streaming has already started.
    /// </summary>
    /// <returns>The result-filter ownership assertions.</returns>
    [Fact]
    public async Task StartedResponse_FilterDoesNotCreateAnotherEnvelope()
    {
        var boundary = new ApiResponseBoundary("/api/reports", "2.5");
        var http = new DefaultHttpContext();
        http.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        http.Request.Path = "/api/reports/missing";
        var action = new ActionContext(http, new RouteData(),
            new ActionDescriptor());
        var result = new NotFoundResult();
        var executing = new ResultExecutingContext(action, [], result, new object());
        var factory = new ApiErrorResponseFactory(new ApiResponseMetadataFactory(TimeProvider.System, boundary));
        var filter = new ApiErrorResponseFilter(boundary, factory, NullLogger<ApiErrorResponseFilter>.Instance);

        // Deliberately omit correlation metadata: a started response must not attempt to construct replacement metadata.
        await filter.OnResultExecutionAsync(executing,
            () => Task.FromResult(new ResultExecutedContext(action, [], executing.Result, executing.Controller)));

        Assert.Same(result, executing.Result);
    }

    /// <summary>
    /// Represents the native server condition after response headers have been sent.
    /// </summary>
    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        /// <summary>
        /// Gets the fixture's already-started state independently of response buffering.
        /// </summary>
        public override bool HasStarted => true;
    }

    /// <summary>
    /// Creates one isolated host with an explicit API prefix and an empty native routing failure.
    /// </summary>
    /// <param name="prefix">The route prefix owned by this fixture host.</param>
    /// <param name="version">The public metadata version selected by this fixture host.</param>
    /// <returns>The started host, disposed by its caller.</returns>
    private static async Task<WebApplication> CreateHost(string prefix, string version)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddApiInfrastructure(prefix, version);
        builder.Services.AddLoggingInfrastructure(new ConfigurationBuilder().Build(), "boundary-fixture", "Test");
        builder.Services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions,
            DependencyInjection.OpenApiRegistrationTests.FixtureAuthenticationHandler>(
                "fixture", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("permission", policy => policy.RequireClaim("permission")));
        builder.Services.AddOpenApi();
        builder.Services.AddOpenApiInfrastructure(registration => registration.ApiErrorResponses());

        var app = builder.Build();
        app.UseCorrelation();
        app.UseRequestLogging();
        app.UseApiErrorHandling();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapOpenApi();
        app.MapGet(prefix + "/protected", () => TypedResults.Ok("allowed")).RequireAuthorization("permission");
        app.MapGet(prefix + "/body", () => Results.Text("unchanged", statusCode: 404));
        app.MapGet(prefix + "/file", () => Results.File(Encoding.UTF8.GetBytes("file-payload"), "application/octet-stream"));
        app.MapGet(prefix + "/failure", _ => throw new InvalidOperationException("private-failure"));
        app.MapFallback(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        await app.StartAsync();
        return app;
    }
}
