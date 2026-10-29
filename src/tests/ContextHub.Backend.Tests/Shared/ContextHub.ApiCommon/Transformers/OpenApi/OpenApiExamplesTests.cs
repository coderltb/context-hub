namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Transformers.OpenApi;

/// <summary>
/// Exercises shared example enrichment through native OpenAPI generation in an actual HTTP host.
/// </summary>
public sealed class OpenApiExamplesTests
{
    /// <summary>
    /// Detects use of minimal-endpoint JSON settings for controller examples.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ControllerExample_UsesMvcJsonNaming()
    {
        await using var app = await CreateHost();
        using var document = await ReadDocument(app);
        var example = document.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/mvc-examples")
            .GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("example");

        Assert.Equal("synthetic", example.GetProperty("displayName").GetString());
        Assert.False(example.TryGetProperty("display_name", out _));
    }

    /// <summary>
    /// Detects missing vendor JSON enrichment and accidental example insertion into text media.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ResponseMedia_EnrichesJsonOnly()
    {
        await using var app = await CreateHost(app =>
            app.MapGet("/api/admin/v1/media-examples", () => Results.Ok())
                .Produces<ExampleRequest>(200, "application/json", "application/vnd.example+json", "text/plain")
                .WithMetadata(new OpenApiResponseExampleAttribute<FirstExample>(200)));
        using var document = await ReadDocument(app);
        var media = document.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/media-examples")
            .GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content");

        Assert.Equal("synthetic", media.GetProperty("application/vnd.example+json").GetProperty("examples")
            .GetProperty("default").GetProperty("value").GetProperty("display_name").GetString());
        Assert.True(media.GetProperty("application/json").TryGetProperty("examples", out _));
        Assert.False(media.GetProperty("text/plain").TryGetProperty("examples", out _));
    }

    /// <summary>
    /// Detects silent replacement of a response variant when two providers use the same name.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task DuplicateResponseNames_RejectDocumentGeneration()
    {
        await using var app = await CreateHost(app =>
            app.MapGet("/api/admin/v1/duplicate", () => Results.Ok())
                .Produces<ExampleRequest>()
                .WithMetadata(new OpenApiResponseExampleAttribute<FirstExample>(200),
                    new OpenApiResponseExampleAttribute<SecondExample>(200)));
        using var client = app.GetTestClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetStringAsync("/openapi/v1.json"));

        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.INVALID_EXAMPLE_NAME, exception.Message);
    }

    /// <summary>
    /// Detects uncontrolled provider activation instead of explicit dependency-injection registration.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task UnregisteredProvider_RejectsDocumentGeneration()
    {
        await using var app = await CreateHost(app =>
            app.MapGet("/api/admin/v1/unregistered", () => Results.Ok())
                .Produces<ExampleRequest>()
                .WithMetadata(new OpenApiResponseExampleAttribute<UnregisteredExample>(200)));
        using var client = app.GetTestClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetStringAsync("/openapi/v1.json"));

        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.PROVIDER_NOT_REGISTERED, exception.Message);
    }

    /// <summary>
    /// Detects unnecessary provider execution for a response that cannot carry a JSON example.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task BodylessResponse_DoesNotResolveProvider()
    {
        await using var app = await CreateHost(app =>
            app.MapDelete("/api/admin/v1/bodyless", () => Results.NoContent())
                .Produces(204)
                .WithMetadata(new OpenApiResponseExampleAttribute<UnregisteredExample>(204)));
        using var document = await ReadDocument(app);
        var response = document.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/bodyless")
            .GetProperty("delete").GetProperty("responses").GetProperty("204");

        Assert.False(response.TryGetProperty("content", out _));
    }

    /// <summary>
    /// Detects missing request enrichment and serialization that diverges from the host's HTTP JSON settings.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task RequestExample_UsesHostJsonNaming()
    {
        await using var app = await CreateHost();
        using var document = await ReadDocument(app);
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/examples").GetProperty("post");
        var example = operation.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("example");

        Assert.Equal("synthetic", example.GetProperty("display_name").GetString());
        Assert.False(example.TryGetProperty("DisplayName", out _));
    }

    /// <summary>
    /// Detects merging of alternative examples or creation of an undocumented response status.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ResponseExamples_PreserveVariantsAndDeclaredStatuses()
    {
        await using var app = await CreateHost();
        using var document = await ReadDocument(app);
        var responses = document.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/examples")
            .GetProperty("post").GetProperty("responses");
        var examples = responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("examples");

        Assert.Equal("synthetic", examples.GetProperty("first").GetProperty("value").GetProperty("display_name").GetString());
        Assert.Equal(7, examples.GetProperty("second").GetProperty("value").GetProperty("count").GetInt32());
        Assert.Equal("second", examples.GetProperty("second").GetProperty("value").GetProperty("display_name").GetString());
        Assert.False(responses.TryGetProperty("409", out _));
    }

    /// <summary>
    /// Detects example metadata leaking into protocol endpoints or an Admin prefix lookalike.
    /// </summary>
    /// <param name="path">The request path or configured path exercised by this scenario.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("/mcp")]
    [InlineData("/connect/token")]
    [InlineData("/api/admin/v10/examples")]
    public async Task NonAdminOperations_AreNotEnriched(string path)
    {
        await using var app = await CreateHost();
        using var document = await ReadDocument(app);
        var media = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json");

        Assert.False(media.TryGetProperty("example", out _));
    }

    /// <summary>
    /// Builds a native OpenAPI host with explicit providers and a deliberately non-default naming policy.
    /// </summary>
    /// <param name="configureEndpoints">Additional endpoints for a focused boundary scenario.</param>
    /// <returns>The started test application; the caller owns its shutdown and disposal.</returns>
    private static async Task<WebApplication> CreateHost(Action<WebApplication>? configureEndpoints = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        builder.Services.AddSingleton<FirstExample>();
        builder.Services.AddSingleton<SecondExample>();
        builder.Services.AddControllers().AddApplicationPart(typeof(OpenApiExampleController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        builder.Services.AddApiInfrastructure("/api/admin/v1", "1.0");
        builder.Services.AddOpenApi(options => options.AddApiExamples());
        var app = builder.Build();
        app.MapOpenApi();
        app.MapControllers();
        app.MapPost("/api/admin/v1/examples", (ExampleRequest request) => Results.Ok(request))
            .Produces<ExampleRequest>()
            .WithMetadata(new OpenApiRequestExampleAttribute<FirstExample>(),
                new OpenApiResponseExampleAttribute<FirstExample>(200) { Name = "first" },
                new OpenApiResponseExampleAttribute<SecondExample>(200) { Name = "second" },
                new OpenApiResponseExampleAttribute<SecondExample>(409));
        foreach (var path in new[] { "/mcp", "/connect/token", "/api/admin/v10/examples" })
        {
            app.MapPost(path, (ExampleRequest request) => Results.Ok(request))
                .WithMetadata(new OpenApiRequestExampleAttribute<FirstExample>());
        }

        configureEndpoints?.Invoke(app);
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Fetches the serialized native document rather than mocking transformer internals.
    /// </summary>
    /// <param name="app">The test application receiving the endpoint configuration.</param>
    /// <returns>The parsed OpenAPI document; the caller owns its disposal.</returns>
    private static async Task<JsonDocument> ReadDocument(WebApplication app)
    {
        using var client = app.GetTestClient();
        var json = await client.GetStringAsync("/openapi/v1.json");
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Defines the synthetic JSON input shape used by the test host.
    /// </summary>
    /// <param name="DisplayName">A non-sensitive example name.</param>
    /// <param name="Count">An optional synthetic count for an alternative fixture.</param>
    public sealed record ExampleRequest(string DisplayName, int? Count = null);

    /// <summary>
    /// Supplies a named object fixture without accessing application data.
    /// </summary>
    public sealed class FirstExample : IOpenApiExampleProvider
    {
        /// <summary>
        /// Creates the first synthetic example.
        /// </summary>
        /// <returns>A synthetic request value.</returns>
        public object GetExample() => new ExampleRequest("synthetic");
    }

    /// <summary>
    /// Supplies a distinct response shape to catch accidental example merging.
    /// </summary>
    public sealed class SecondExample : IOpenApiExampleProvider
    {
        /// <summary>
        /// Creates the second synthetic example.
        /// </summary>
        /// <returns>A synthetic count response.</returns>
        public object GetExample() => new ExampleRequest("second", 7);
    }

    /// <summary>
    /// Represents a provider that metadata may reference but the host deliberately does not register.
    /// </summary>
    public sealed class UnregisteredExample : IOpenApiExampleProvider
    {
        /// <summary>
        /// Creates a fixture only if an incorrect implementation bypasses DI registration.
        /// </summary>
        /// <returns>A synthetic object.</returns>
        public object GetExample() => new { Value = "must-not-be-activated" };
    }
}

/// <summary>
/// Supplies a controller endpoint to verify MVC-specific JSON serialization during OpenAPI enrichment.
/// </summary>
[ApiController]
[Route("api/admin/v1/mvc-examples")]
public sealed class OpenApiExampleController : ControllerBase
{
    /// <summary>
    /// Accepts a synthetic value without accessing external services or application data.
    /// </summary>
    /// <param name="request">The synthetic input.</param>
    /// <returns>The same value for documentation generation.</returns>
    [HttpPost]
    [OpenApiRequestExample<OpenApiExamplesTests.FirstExample>]
    public ActionResult<OpenApiExamplesTests.ExampleRequest> Create(OpenApiExamplesTests.ExampleRequest request) => Ok(request);
}
